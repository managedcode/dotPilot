using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed partial record ChatSessionView(
    string Title,
    string StatusSummary,
    IReadOnlyList<ChatTimelineItem> Messages,
    IReadOnlyList<ParticipantItem> Members,
    IReadOnlyList<ParticipantItem> Agents);

[Bindable]
public sealed partial record AgentBuilderView(
    string ProviderDisplayName,
    string ProviderStatusSummary,
    string ProviderCommandName,
    string ProviderVersionLabel,
    bool HasProviderVersion,
    string SuggestedModelName,
    string StatusMessage,
    bool CanCreateAgent);
