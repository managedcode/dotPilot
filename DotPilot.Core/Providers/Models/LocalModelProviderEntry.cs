namespace DotPilot.Core.Providers;

internal sealed record LocalModelProviderEntry(
    string ModelName,
    string BaseModelName,
    string ModelPath,
    DateTimeOffset AddedAt,
    string? DetectedRuntimeType);
