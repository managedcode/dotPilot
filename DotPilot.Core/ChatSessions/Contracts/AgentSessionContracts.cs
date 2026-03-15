using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.ChatSessions.Contracts;

public sealed record ProviderActionDescriptor(
    string Label,
    string Summary,
    string Command);

public sealed record ProviderStatusDescriptor(
    ProviderId Id,
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    AgentProviderStatus Status,
    string StatusSummary,
    string? InstalledVersion,
    bool IsEnabled,
    bool CanCreateAgents,
    IReadOnlyList<ProviderActionDescriptor> Actions);

public sealed record AgentProfileSummary(
    AgentProfileId Id,
    string Name,
    AgentProviderKind ProviderKind,
    string ProviderDisplayName,
    string ModelName,
    string SystemPrompt,
    DateTimeOffset CreatedAt);

public sealed record SessionListItem(
    SessionId Id,
    string Title,
    string Preview,
    string StatusSummary,
    DateTimeOffset UpdatedAt,
    AgentProfileId PrimaryAgentId,
    string PrimaryAgentName,
    string ProviderDisplayName);

public sealed record SessionStreamEntry(
    string Id,
    SessionId SessionId,
    SessionStreamEntryKind Kind,
    string Author,
    string Text,
    DateTimeOffset Timestamp,
    AgentProfileId? AgentProfileId = null,
    string? AccentLabel = null);

public sealed record SessionTranscriptSnapshot(
    SessionListItem Session,
    IReadOnlyList<SessionStreamEntry> Entries,
    IReadOnlyList<AgentProfileSummary> Participants);

public sealed record AgentWorkspaceSnapshot(
    IReadOnlyList<SessionListItem> Sessions,
    IReadOnlyList<AgentProfileSummary> Agents,
    IReadOnlyList<ProviderStatusDescriptor> Providers,
    SessionId? SelectedSessionId);
