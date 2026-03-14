using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.RuntimeFoundation;

public sealed record AgentTurnResumeRequest(
    SessionId SessionId,
    ApprovalState ApprovalState,
    string Summary);

public sealed record RuntimeSessionReplayEntry(
    string Kind,
    string Summary,
    SessionPhase Phase,
    ApprovalState ApprovalState,
    DateTimeOffset RecordedAt);

public sealed record RuntimeSessionArchive(
    SessionId SessionId,
    string WorkflowSessionId,
    SessionPhase Phase,
    ApprovalState ApprovalState,
    DateTimeOffset UpdatedAt,
    string? CheckpointId,
    IReadOnlyList<RuntimeSessionReplayEntry> Replay,
    IReadOnlyList<ArtifactDescriptor> Artifacts);
