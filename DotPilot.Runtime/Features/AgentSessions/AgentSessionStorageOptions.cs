namespace DotPilot.Runtime.Features.AgentSessions;

public sealed class AgentSessionStorageOptions
{
    public bool UseInMemoryDatabase { get; init; }

    public string InMemoryDatabaseName { get; init; } = "DotPilotAgentSessions";

    public string? DatabasePath { get; init; }

    public string? RuntimeSessionDirectoryPath { get; init; }

    public string? ChatHistoryDirectoryPath { get; init; }
}
