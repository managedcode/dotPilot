using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed partial record ChatSessionView(
    string Initial,
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
    IReadOnlyList<string> SupportedModelNames,
    bool HasSupportedModels,
    string ModelHelperText,
    string StatusMessage,
    bool CanCreateAgent);
