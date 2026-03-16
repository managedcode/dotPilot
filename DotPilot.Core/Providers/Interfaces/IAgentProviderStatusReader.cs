namespace DotPilot.Core.Providers.Interfaces;

public interface IAgentProviderStatusReader
{
    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> ReadAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> RefreshAsync(CancellationToken cancellationToken);

    void Invalidate();
}
