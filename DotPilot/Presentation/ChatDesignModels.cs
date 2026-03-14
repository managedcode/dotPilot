using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed class SessionSidebarItem(SessionId id, string title, string preview) : ObservableObject
{
    private string _preview = preview;

    public SessionId Id { get; } = id;

    public string Title { get; } = title;

    public string Preview
    {
        get => _preview;
        set => SetProperty(ref _preview, value);
    }
}

[Bindable]
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

[Bindable]
public sealed partial record ParticipantItem(
    string Name,
    string SecondaryText,
    string Initial,
    Brush? AvatarBrush,
    string? BadgeText = null,
    Brush? BadgeBrush = null);

[Bindable]
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

[Bindable]
public sealed partial record ProviderActionItem(
    string Label,
    string Summary,
    string Command);
