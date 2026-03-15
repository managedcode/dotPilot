
namespace DotPilot.Core.Providers;

internal sealed record ProviderStatusCacheSnapshot(
    IReadOnlyList<ProviderStatusDescriptor> Providers,
    DateTimeOffset CreatedAt);
