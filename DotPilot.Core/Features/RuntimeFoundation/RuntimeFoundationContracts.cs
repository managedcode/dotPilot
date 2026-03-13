namespace DotPilot.Core.Features.RuntimeFoundation;

public sealed record RuntimeSliceDescriptor(
    int IssueNumber,
    string IssueLabel,
    string Name,
    string Summary,
    RuntimeSliceState State);

public sealed record ProviderToolchainStatus(
    string DisplayName,
    string CommandName,
    ProviderConnectionStatus Status,
    string StatusSummary,
    bool RequiresExternalToolchain);

public sealed record RuntimeFoundationSnapshot(
    string EpicLabel,
    string Summary,
    string DeterministicClientName,
    string DeterministicProbePrompt,
    IReadOnlyList<RuntimeSliceDescriptor> Slices,
    IReadOnlyList<ProviderToolchainStatus> Providers);

public sealed record AgentTurnRequest(
    SessionId SessionId,
    AgentProfileId AgentProfileId,
    string Prompt,
    AgentExecutionMode Mode);

public sealed record AgentTurnResult(
    string Summary,
    SessionPhase NextPhase,
    ApprovalState ApprovalState,
    IReadOnlyList<string> ProducedArtifacts);
