
namespace DotPilot.Core.Providers;

internal sealed record ProviderStatusProbeResult(
    ProviderStatusDescriptor Descriptor,
    string? ExecutablePath);
