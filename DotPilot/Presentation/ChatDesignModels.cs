using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Presentation;

public sealed partial record SessionSidebarItem(
    SessionId Id,
    string Title,
    string Preview);

public sealed partial record ChatTimelineItem(
    string Id,
    SessionStreamEntryKind Kind,
    string Author,
    string Timestamp,
    string Content,
    string Initial,
    Brush? AvatarBrush,
    bool IsCurrentUser,
    string? AccentLabel = null);

public sealed partial record ParticipantItem(
    string Name,
    string SecondaryText,
    string Initial,
    Brush? AvatarBrush,
    string? BadgeText = null,
    Brush? BadgeBrush = null);

public sealed partial record ProviderStatusItem(
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    string StatusSummary,
    string? InstalledVersion,
    bool IsEnabled,
    bool CanCreateAgents,
    string InstallCommand,
    IReadOnlyList<ProviderActionItem> Actions);

public sealed partial record ProviderActionItem(
    string Label,
    string Summary,
    string Command);
