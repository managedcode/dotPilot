namespace DotPilot.Core.ChatSessions.Models;

public enum AgentProviderKind
{
    Debug,
    Codex,
    ClaudeCode,
    GitHubCopilot,
    Gemini,
}

public enum AgentProviderStatus
{
    Ready,
    RequiresSetup,
    Disabled,
    Unsupported,
    Error,
}

public enum SessionStreamEntryKind
{
    UserMessage,
    AssistantMessage,
    ToolStarted,
    ToolCompleted,
    Status,
    Error,
}
