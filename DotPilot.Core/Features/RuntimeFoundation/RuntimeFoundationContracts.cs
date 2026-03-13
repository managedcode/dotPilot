using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.RuntimeFoundation;

public sealed record RuntimeSliceDescriptor(
    int IssueNumber,
    string IssueLabel,
    string Name,
    string Summary,
    RuntimeSliceState State);

public sealed record RuntimeFoundationSnapshot(
    string EpicLabel,
    string Summary,
    string DeterministicClientName,
    string DeterministicProbePrompt,
    IReadOnlyList<RuntimeSliceDescriptor> Slices,
    IReadOnlyList<ProviderDescriptor> Providers);

public sealed record AgentTurnRequest(
    SessionId SessionId,
    AgentProfileId AgentProfileId,
    string Prompt,
    AgentExecutionMode Mode,
    ProviderConnectionStatus ProviderStatus = ProviderConnectionStatus.Available);

public sealed record AgentTurnResult(
    string Summary,
    SessionPhase NextPhase,
    ApprovalState ApprovalState,
    IReadOnlyList<ArtifactDescriptor> ProducedArtifacts);
