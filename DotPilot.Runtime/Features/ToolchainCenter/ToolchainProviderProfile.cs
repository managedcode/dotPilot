namespace DotPilot.Runtime.Features.ToolchainCenter;

internal sealed record ToolchainProviderProfile(
    int IssueNumber,
    string SectionLabel,
    string DisplayName,
    string CommandName,
    IReadOnlyList<string> VersionArguments,
    IReadOnlyList<string> ToolAccessArguments,
    string ToolAccessDiagnosticName,
    string ToolAccessReadySummary,
    string ToolAccessBlockedSummary,
    IReadOnlyList<string> AuthenticationEnvironmentVariables,
    IReadOnlyList<ToolchainConfigurationSignal> ConfigurationSignals);
