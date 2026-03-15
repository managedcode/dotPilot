namespace DotPilot.Core.Providers.Interfaces;

public interface IAgentProviderStatusReader
{
    ValueTask<IReadOnlyList<ProviderStatusDescriptor>> ReadAsync(CancellationToken cancellationToken);
}
