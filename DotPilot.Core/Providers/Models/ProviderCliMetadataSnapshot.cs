namespace DotPilot.Core.Providers;

internal sealed record ProviderCliMetadataSnapshot(
    string? InstalledVersion,
    string? SuggestedModelName,
    IReadOnlyList<string> SupportedModels);
