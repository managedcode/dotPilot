namespace DotPilot.Core.Features.AgentSessions;

public enum AgentProviderKind
{
    Debug,
    Codex,
    ClaudeCode,
    GitHubCopilot,
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

