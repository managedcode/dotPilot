namespace DotPilot.Core.Providers;

internal sealed record LocalModelCompatibilityInfo(
    string? NormalizedModelPath,
    bool IsCompatible,
    string? SuggestedModelName,
    string? FailureCode,
    string? FailureMessage,
    string? DetectedRuntimeType,
    IReadOnlyList<string> SupportedRuntimeTypes);
