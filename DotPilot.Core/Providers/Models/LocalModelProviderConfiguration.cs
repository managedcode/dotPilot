namespace DotPilot.Core.Providers;

internal sealed record LocalModelProviderConfiguration(
    string PrimaryEnvironmentVariableName,
    IReadOnlyList<string> EnvironmentVariableNames,
    string SetupCommand,
    IReadOnlyList<LocalModelProviderEntry> Models,
    IReadOnlyList<string> ConfiguredModelPaths,
    bool IsReady,
    string? SuggestedModelName,
    IReadOnlyList<string> SupportedModelNames,
    string? ValidationErrorCode,
    string? ValidationErrorMessage,
    IReadOnlyList<string> DetectedRuntimeTypes,
    IReadOnlyList<string> SupportedRuntimeTypes)
{
    public string? ModelPath => ResolveModelPath(SuggestedModelName);

    public string? DetectedRuntimeType => DetectedRuntimeTypes.Count == 0 ? null : DetectedRuntimeTypes[0];

    public string? ResolveModelPath(string? modelName)
    {
        if (Models.Count == 0)
        {
            return ConfiguredModelPaths.Count == 0 ? null : ConfiguredModelPaths[0];
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return Models[0].ModelPath;
        }

        var directMatch = Models.FirstOrDefault(entry =>
            string.Equals(entry.ModelName, modelName, StringComparison.OrdinalIgnoreCase));
        if (directMatch is not null)
        {
            return directMatch.ModelPath;
        }

        var baseMatches = Models
            .Where(entry => string.Equals(entry.BaseModelName, modelName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (baseMatches.Length == 1)
        {
            return baseMatches[0].ModelPath;
        }

        return Models[0].ModelPath;
    }
}
