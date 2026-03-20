namespace DotPilot.Core.Providers;

internal sealed record LocalModelProviderConfiguration(
    string PrimaryEnvironmentVariableName,
    IReadOnlyList<string> EnvironmentVariableNames,
    string SetupCommand,
    string? ModelPath,
    bool IsReady,
    string? SuggestedModelName);
