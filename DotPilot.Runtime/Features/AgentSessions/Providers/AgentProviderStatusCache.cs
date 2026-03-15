using System.Diagnostics;
using DotPilot.Core.Features.AgentSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class AgentProviderStatusCache(
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<AgentProviderStatusCache> logger)
    : IAgentProviderStatusCache, IDisposable
{
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromMinutes(5);
    private const string MissingValue = "<none>";
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private ProviderStatusSnapshot? _snapshot;

    public ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        return GetSnapshotCoreAsync(forceRefresh: false, cancellationToken);
    }

    public ValueTask<IReadOnlyList<ProviderStatusDescriptor>> RefreshAsync(CancellationToken cancellationToken)
    {
        return GetSnapshotCoreAsync(forceRefresh: true, cancellationToken);
    }

    public void Dispose()
    {
        _refreshGate.Dispose();
    }

    private async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetSnapshotCoreAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var snapshot = _snapshot;
        if (!forceRefresh && snapshot is not null && !IsExpired(snapshot))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var cacheAgeMilliseconds = (timeProvider.GetUtcNow() - snapshot.CreatedAt).TotalMilliseconds;
                AgentProviderStatusCacheLog.CacheHit(
                    logger,
                    cacheAgeMilliseconds);
            }

            return snapshot.Providers;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            snapshot = _snapshot;
            if (!forceRefresh && snapshot is not null && !IsExpired(snapshot))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    var cacheAgeMilliseconds = (timeProvider.GetUtcNow() - snapshot.CreatedAt).TotalMilliseconds;
                    AgentProviderStatusCacheLog.CacheHit(
                        logger,
                        cacheAgeMilliseconds);
                }

                return snapshot.Providers;
            }

            var startedAt = Stopwatch.GetTimestamp();
            AgentProviderStatusCacheLog.RefreshStarted(logger, forceRefresh);

            try
            {
                await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
                var probeResults = await AgentProviderStatusSnapshotReader.BuildAsync(dbContext, cancellationToken);
                var providers = probeResults
                    .Select(result => result.Descriptor)
                    .ToArray();

                _snapshot = new ProviderStatusSnapshot(providers, timeProvider.GetUtcNow());

                foreach (var probeResult in probeResults)
                {
                    AgentProviderStatusCacheLog.ProbeCompleted(
                        logger,
                        probeResult.Descriptor.Kind,
                        probeResult.Descriptor.Status,
                        probeResult.Descriptor.IsEnabled,
                        probeResult.Descriptor.CanCreateAgents,
                        probeResult.Descriptor.InstalledVersion ?? MissingValue,
                        probeResult.ExecutablePath ?? MissingValue);
                }

                if (logger.IsEnabled(LogLevel.Information))
                {
                    var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                    AgentProviderStatusCacheLog.RefreshCompleted(
                        logger,
                        providers.Length,
                        elapsedMilliseconds);
                }

                return providers;
            }
            catch (Exception exception)
            {
                AgentProviderStatusCacheLog.RefreshFailed(logger, exception);
                throw;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool IsExpired(ProviderStatusSnapshot snapshot)
    {
        return timeProvider.GetUtcNow() - snapshot.CreatedAt >= SnapshotLifetime;
    }

    private sealed record ProviderStatusSnapshot(
        IReadOnlyList<ProviderStatusDescriptor> Providers,
        DateTimeOffset CreatedAt);
}
