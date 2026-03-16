namespace DotPilot.Core.ChatSessions;

internal sealed record LocalCodexThreadState(
    string ThreadId,
    string WorkingDirectory,
    string ModelName,
    string SystemPromptHash,
    bool InstructionsSeeded);
