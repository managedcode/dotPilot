namespace DotPilot.Core.ChatSessions;

internal sealed class AgentProfileRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Role { get; set; }

    public int ProviderKind { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public string CapabilitiesJson { get; set; } = AgentProfileSchemaDefaults.EmptyCapabilitiesJson;

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class SessionRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid PrimaryAgentProfileId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SessionEntryRecord
{
    public string Id { get; set; } = string.Empty;

    public Guid SessionId { get; set; }

    public Guid? AgentProfileId { get; set; }

    public int Kind { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? AccentLabel { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}

internal sealed class ProviderPreferenceRecord
{
    public int ProviderKind { get; set; }

    public bool IsEnabled { get; set; }

    public string? LocalModelPath { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class ProviderLocalModelRecord
{
    public int ProviderKind { get; set; }

    public string ModelPath { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; }
}
