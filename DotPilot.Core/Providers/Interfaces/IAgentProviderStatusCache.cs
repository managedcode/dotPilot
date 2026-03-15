
namespace DotPilot.Core.Providers.Interfaces;

public interface IAgentProviderStatusCache
{
    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetSnapshotAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> RefreshAsync(CancellationToken cancellationToken);
}
