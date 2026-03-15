
namespace DotPilot.Core.Providers;

internal static class AgentSessionProviderCatalog
{
    private const string DebugDisplayName = "Debug Provider";
    private const string DebugCommandName = "debug";
    private const string DebugModelName = "debug-echo";
    private const string DebugInstallCommand = "built-in";

    private const string CodexDisplayName = "Codex";
    private const string CodexCommandName = "codex";
    private const string CodexModelName = "gpt-5";
    private const string CodexInstallCommand = "npm install -g @openai/codex";

    private const string ClaudeDisplayName = "Claude Code";
    private const string ClaudeCommandName = "claude";
    private const string ClaudeModelName = "claude-sonnet-4-5";
    private const string ClaudeInstallCommand = "npm install -g @anthropic-ai/claude-code";

    private const string CopilotDisplayName = "GitHub Copilot";
    private const string CopilotCommandName = "copilot";
    private const string CopilotModelName = "gpt-5";
    private const string CopilotInstallCommand = "npm install -g @github/copilot";

    private static readonly IReadOnlyDictionary<AgentProviderKind, AgentSessionProviderProfile> ProfilesByKind =
        CreateProfiles()
            .ToDictionary(profile => profile.Kind);

    public static IReadOnlyList<AgentSessionProviderProfile> All => [.. ProfilesByKind.Values];

    public static AgentSessionProviderProfile Get(AgentProviderKind kind) => ProfilesByKind[kind];

    private static IReadOnlyList<AgentSessionProviderProfile> CreateProfiles()
    {
        return
        [
            new(AgentProviderKind.Debug, DebugDisplayName, DebugCommandName, DebugModelName, DebugInstallCommand, true, true),
            new(AgentProviderKind.Codex, CodexDisplayName, CodexCommandName, CodexModelName, CodexInstallCommand, false, true),
            new(AgentProviderKind.ClaudeCode, ClaudeDisplayName, ClaudeCommandName, ClaudeModelName, ClaudeInstallCommand, false, false),
            new(AgentProviderKind.GitHubCopilot, CopilotDisplayName, CopilotCommandName, CopilotModelName, CopilotInstallCommand, false, false),
        ];
    }
}
