using System.Diagnostics;
using DotPilot.Core.ChatSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.Providers;

internal sealed class AgentProviderStatusReader(
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    ILogger<AgentProviderStatusReader> logger)
    : IAgentProviderStatusReader
{
    private const string MissingValue = "<none>";
    private readonly object activeReadSync = new();
    private IReadOnlyList<ProviderStatusDescriptor>? cachedSnapshot;
    private Task<IReadOnlyList<ProviderStatusDescriptor>>? activeReadTask;
    private long activeReadGeneration = -1;
    private long snapshotGeneration;

    public async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readTask = GetOrStartActiveRead(forceRefresh: false);
            return await readTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentProviderStatusReaderLog.ReadFailed(logger, exception);
            throw;
        }
    }

    public async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readTask = GetOrStartActiveRead(forceRefresh: true);
            return await readTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentProviderStatusReaderLog.ReadFailed(logger, exception);
            throw;
        }
    }

    public void Invalidate()
    {
        lock (activeReadSync)
        {
            snapshotGeneration++;
            cachedSnapshot = null;
        }
    }

    private Task<IReadOnlyList<ProviderStatusDescriptor>> GetOrStartActiveRead(bool forceRefresh)
    {
        Task<IReadOnlyList<ProviderStatusDescriptor>>? activeTask;
        long generation;

        lock (activeReadSync)
        {
            if (!forceRefresh && cachedSnapshot is { } snapshot)
            {
                return Task.FromResult(snapshot);
            }

            if (!forceRefresh &&
                activeReadTask is { IsCompleted: false } &&
                activeReadGeneration == snapshotGeneration)
            {
                return activeReadTask;
            }

            if (forceRefresh)
            {
                snapshotGeneration++;
                cachedSnapshot = null;
            }

            generation = snapshotGeneration;
            activeReadGeneration = generation;
            activeReadTask = CreateReadTask(generation);
            activeTask = activeReadTask;
        }

        return activeTask;
    }

    private Task<IReadOnlyList<ProviderStatusDescriptor>> CreateReadTask(long generation)
    {
        Task<IReadOnlyList<ProviderStatusDescriptor>>? readTask = null;
        readTask = ReadAndCacheAsync();
        return readTask;

        async Task<IReadOnlyList<ProviderStatusDescriptor>> ReadAndCacheAsync()
        {
            try
            {
                var snapshot = await ReadFromCurrentSourcesAsync();
                lock (activeReadSync)
                {
                    if (generation == snapshotGeneration)
                    {
                        cachedSnapshot = snapshot;
                    }
                }

                return snapshot;
            }
            finally
            {
                lock (activeReadSync)
                {
                    if (ReferenceEquals(activeReadTask, readTask))
                    {
                        activeReadTask = null;
                        activeReadGeneration = -1;
                    }
                }
            }
        }
    }

    private async Task<IReadOnlyList<ProviderStatusDescriptor>> ReadFromCurrentSourcesAsync()
    {
        var startedAt = Stopwatch.GetTimestamp();
        AgentProviderStatusReaderLog.ReadStarted(logger);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(CancellationToken.None);
        var probeResults = await AgentProviderStatusSnapshotReader.BuildAsync(dbContext, CancellationToken.None);
        var providers = probeResults
            .Select(result => result.Descriptor)
            .ToArray();

        foreach (var probeResult in probeResults)
        {
            AgentProviderStatusReaderLog.ProbeCompleted(
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
            AgentProviderStatusReaderLog.ReadCompleted(
                logger,
                providers.Length,
                elapsedMilliseconds);
        }

        return providers;
    }
}
