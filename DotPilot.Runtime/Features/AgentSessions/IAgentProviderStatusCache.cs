using DotPilot.Core.Features.AgentSessions;

namespace DotPilot.Runtime.Features.AgentSessions;

public interface IAgentProviderStatusCache
{
    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetSnapshotAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> RefreshAsync(CancellationToken cancellationToken);
}
