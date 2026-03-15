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
    private Task<IReadOnlyList<ProviderStatusDescriptor>>? activeReadTask;

    public async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readTask = GetOrStartActiveRead();
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
        finally
        {
            ClearCompletedActiveRead();
        }
    }

    private Task<IReadOnlyList<ProviderStatusDescriptor>> GetOrStartActiveRead()
    {
        lock (activeReadSync)
        {
            if (activeReadTask is { IsCompleted: false })
            {
                return activeReadTask;
            }

            activeReadTask = ReadFromCurrentSourcesAsync();
            return activeReadTask;
        }
    }

    private void ClearCompletedActiveRead()
    {
        lock (activeReadSync)
        {
            if (activeReadTask is { IsCompleted: true })
            {
                activeReadTask = null;
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
