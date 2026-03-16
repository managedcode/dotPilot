namespace DotPilot.Core.Providers;

internal static class AgentProviderKindExtensions
{
    private static readonly IReadOnlyList<string> DebugModels =
    [
        "debug-echo",
    ];

    private static readonly IReadOnlyList<string> CodexModels =
    [
        "gpt-5",
    ];

    private static readonly IReadOnlyList<string> ClaudeModels =
    [
        "claude-opus-4-6",
        "claude-opus-4-5",
        "claude-sonnet-4-5",
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

    public static string GetCommandName(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => "debug",
            AgentProviderKind.Codex => "codex",
            AgentProviderKind.ClaudeCode => "claude",
            AgentProviderKind.GitHubCopilot => "copilot",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetDefaultModelName(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => "debug-echo",
            AgentProviderKind.Codex => "gpt-5",
            AgentProviderKind.ClaudeCode => "claude-sonnet-4-5",
            AgentProviderKind.GitHubCopilot => "gpt-5",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetDisplayName(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => "Debug Provider",
            AgentProviderKind.Codex => "Codex",
            AgentProviderKind.ClaudeCode => "Claude Code",
            AgentProviderKind.GitHubCopilot => "GitHub Copilot",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetInstallCommand(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => "built-in",
            AgentProviderKind.Codex => "npm install -g @openai/codex",
            AgentProviderKind.ClaudeCode => "npm install -g @anthropic-ai/claude-code",
            AgentProviderKind.GitHubCopilot => "npm install -g @github/copilot",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static IReadOnlyList<string> GetSupportedModelNames(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => DebugModels,
            AgentProviderKind.Codex => CodexModels,
            AgentProviderKind.ClaudeCode => ClaudeModels,
            AgentProviderKind.GitHubCopilot => CopilotModels,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static bool IsBuiltIn(this AgentProviderKind kind)
    {
        return kind == AgentProviderKind.Debug;
    }
}
