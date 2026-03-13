using DotPilot.Core.Features.ToolchainCenter;
using DotPilot.Runtime.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal static class ToolchainProviderProfiles
{
    private const string OpenAiApiKey = "OPENAI_API_KEY";
    private const string OpenAiBaseUrl = "OPENAI_BASE_URL";
    private const string AnthropicApiKey = "ANTHROPIC_API_KEY";
    private const string AnthropicBaseUrl = "ANTHROPIC_BASE_URL";
    private const string GitHubToken = "GITHUB_TOKEN";
    private const string GitHubHostToken = "GH_TOKEN";
    private const string GitHubModelsApiKey = "GITHUB_MODELS_API_KEY";
    private const string OpenAiApiKeySummary = "Required secret for Codex-ready non-interactive sessions.";
    private const string OpenAiBaseUrlSummary = "Optional endpoint override for Codex-compatible deployments.";
    private const string AnthropicApiKeySummary = "Required secret for Claude Code non-interactive sessions.";
    private const string AnthropicBaseUrlSummary = "Optional endpoint override for Claude-compatible routing.";
    private const string GitHubTokenSummary = "GitHub token for Copilot and GitHub CLI authenticated flows.";
    private const string GitHubHostTokenSummary = "Alternative GitHub host token for CLI-authenticated Copilot flows.";
    private const string GitHubModelsApiKeySummary = "Optional BYOK key for GitHub Models-backed Copilot routing.";
    private static readonly string[] VersionArguments = ["--version"];

    public static IReadOnlyList<ToolchainProviderProfile> All { get; } =
    [
        new(
            ToolchainCenterIssues.CodexReadiness,
            ProviderToolchainNames.CodexDisplayName,
            ProviderToolchainNames.CodexCommandName,
            VersionArguments,
            ToolAccessArguments: [],
            ToolAccessDiagnosticName: "Tool access",
            ToolAccessReadySummary: "The Codex CLI command surface is reachable for session startup.",
            ToolAccessBlockedSummary: "Install Codex CLI before tool access can be validated.",
            AuthenticationEnvironmentVariables: [OpenAiApiKey],
            ConfigurationSignals:
            [
                new(OpenAiApiKey, OpenAiApiKeySummary, ToolchainConfigurationKind.Secret, IsSensitive: true, IsRequiredForReadiness: true),
                new(OpenAiBaseUrl, OpenAiBaseUrlSummary, ToolchainConfigurationKind.EnvironmentVariable, IsSensitive: false, IsRequiredForReadiness: false),
            ]),
        new(
            ToolchainCenterIssues.ClaudeCodeReadiness,
            ProviderToolchainNames.ClaudeCodeDisplayName,
            ProviderToolchainNames.ClaudeCodeCommandName,
            VersionArguments,
            ToolAccessArguments: [],
            ToolAccessDiagnosticName: "MCP surface",
            ToolAccessReadySummary: "Claude Code is installed and can expose its MCP-oriented CLI surface.",
            ToolAccessBlockedSummary: "Install Claude Code before MCP-oriented checks can run.",
            AuthenticationEnvironmentVariables: [AnthropicApiKey],
            ConfigurationSignals:
            [
                new(AnthropicApiKey, AnthropicApiKeySummary, ToolchainConfigurationKind.Secret, IsSensitive: true, IsRequiredForReadiness: true),
                new(AnthropicBaseUrl, AnthropicBaseUrlSummary, ToolchainConfigurationKind.EnvironmentVariable, IsSensitive: false, IsRequiredForReadiness: false),
            ]),
        new(
            ToolchainCenterIssues.GitHubCopilotReadiness,
            ProviderToolchainNames.GitHubCopilotDisplayName,
            ProviderToolchainNames.GitHubCopilotCommandName,
            VersionArguments,
            ToolAccessArguments: ["copilot", "--help"],
            ToolAccessDiagnosticName: "Copilot command group",
            ToolAccessReadySummary: "GitHub CLI exposes the Copilot command group for SDK-first adapter work.",
            ToolAccessBlockedSummary: "GitHub CLI is present, but the Copilot command group is not available yet.",
            AuthenticationEnvironmentVariables: [GitHubHostToken, GitHubToken],
            ConfigurationSignals:
            [
                new(GitHubHostToken, GitHubHostTokenSummary, ToolchainConfigurationKind.Secret, IsSensitive: true, IsRequiredForReadiness: true),
                new(GitHubToken, GitHubTokenSummary, ToolchainConfigurationKind.Secret, IsSensitive: true, IsRequiredForReadiness: true),
                new(GitHubModelsApiKey, GitHubModelsApiKeySummary, ToolchainConfigurationKind.Secret, IsSensitive: true, IsRequiredForReadiness: false),
            ]),
    ];
}
