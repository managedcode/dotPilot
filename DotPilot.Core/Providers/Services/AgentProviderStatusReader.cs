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

    public async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> ReadAsync(CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        AgentProviderStatusReaderLog.ReadStarted(logger);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var probeResults = await AgentProviderStatusSnapshotReader.BuildAsync(dbContext, cancellationToken);
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
        catch (Exception exception)
        {
            AgentProviderStatusReaderLog.ReadFailed(logger, exception);
            throw;
        }
    }
}
