using ManagedCode.GeminiSharpSDK.Models;

namespace DotPilot.Core.Providers;

internal static class AgentProviderKindExtensions
{
    public static string GetCommandName(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => "debug",
            AgentProviderKind.Codex => "codex",
            AgentProviderKind.ClaudeCode => "claude",
            AgentProviderKind.GitHubCopilot => "copilot",
            AgentProviderKind.Gemini => "gemini",
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
            AgentProviderKind.Gemini => GeminiModels.Gemini25Pro,
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
            AgentProviderKind.Gemini => "Gemini",
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
            AgentProviderKind.Gemini => "npm install -g @google/gemini-cli",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static IReadOnlyList<string> GetSupportedModelNames(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Debug => [kind.GetDefaultModelName()],
            AgentProviderKind.Codex => [],
            AgentProviderKind.ClaudeCode => [],
            AgentProviderKind.GitHubCopilot => [],
            AgentProviderKind.Gemini =>
            [
                GeminiModels.Gemini25Pro,
                GeminiModels.Gemini25Flash,
                GeminiModels.Gemini25FlashLite,
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static bool IsBuiltIn(this AgentProviderKind kind)
    {
        return kind == AgentProviderKind.Debug;
    }
}
