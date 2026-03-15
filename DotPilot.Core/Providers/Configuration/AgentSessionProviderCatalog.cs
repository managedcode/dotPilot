
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

    private static readonly IReadOnlyList<string> DebugModels =
    [
        DebugModelName,
    ];

    private static readonly IReadOnlyList<string> CodexModels =
    [
        CodexModelName,
    ];

    private static readonly IReadOnlyList<string> ClaudeModels =
    [
        "claude-opus-4-6",
        "claude-opus-4-5",
        ClaudeModelName,
        "claude-haiku-4-5",
        "claude-sonnet-4",
    ];

    private static readonly IReadOnlyList<string> CopilotModels =
    [
        "claude-sonnet-4.6",
        "claude-sonnet-4.5",
        "claude-haiku-4.5",
        "claude-opus-4.6",
        "claude-opus-4.6-fast",
        "claude-opus-4.5",
        "claude-sonnet-4",
        "gemini-3-pro-preview",
        "gpt-5.4",
        "gpt-5.3-codex",
        "gpt-5.2-codex",
        "gpt-5.2",
        "gpt-5.1-codex-max",
        "gpt-5.1-codex",
        "gpt-5.1",
        "gpt-5.1-codex-mini",
        "gpt-5-mini",
        "gpt-4.1",
    ];

    private static readonly IReadOnlyDictionary<AgentProviderKind, AgentSessionProviderProfile> ProfilesByKind =
        CreateProfiles()
            .ToDictionary(profile => profile.Kind);

    public static IReadOnlyList<AgentSessionProviderProfile> All => [.. ProfilesByKind.Values];

    public static AgentSessionProviderProfile Get(AgentProviderKind kind) => ProfilesByKind[kind];

    private static IReadOnlyList<AgentSessionProviderProfile> CreateProfiles()
    {
        return
        [
            new(AgentProviderKind.Debug, DebugDisplayName, DebugCommandName, DebugModelName, DebugModels, DebugInstallCommand, true, true),
            new(AgentProviderKind.Codex, CodexDisplayName, CodexCommandName, CodexModelName, CodexModels, CodexInstallCommand, false, true),
            new(AgentProviderKind.ClaudeCode, ClaudeDisplayName, ClaudeCommandName, ClaudeModelName, ClaudeModels, ClaudeInstallCommand, false, false),
            new(AgentProviderKind.GitHubCopilot, CopilotDisplayName, CopilotCommandName, CopilotModelName, CopilotModels, CopilotInstallCommand, false, false),
        ];
    }
}
