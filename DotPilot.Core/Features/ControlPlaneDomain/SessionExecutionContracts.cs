namespace DotPilot.Core.Features.ControlPlaneDomain;

[GenerateSerializer]
public sealed record SessionDescriptor
{
    [Id(0)]
    public SessionId Id { get; init; }

    [Id(1)]
    public WorkspaceId WorkspaceId { get; init; }

    [Id(2)]
    public string Title { get; init; } = string.Empty;

    [Id(3)]
    public SessionPhase Phase { get; init; } = SessionPhase.Plan;

    [Id(4)]
    public ApprovalState ApprovalState { get; init; } = ApprovalState.NotRequired;

    [Id(5)]
    public FleetId? FleetId { get; init; }

    [Id(6)]
    public AgentProfileId[] AgentProfileIds { get; init; } = [];

    [Id(7)]
    public DateTimeOffset CreatedAt { get; init; }

    [Id(8)]
    public DateTimeOffset UpdatedAt { get; init; }
}

[GenerateSerializer]
public sealed record SessionApprovalRecord
{
    [Id(0)]
    public ApprovalId Id { get; init; }

    [Id(1)]
    public SessionId SessionId { get; init; }

    [Id(2)]
    public ApprovalScope Scope { get; init; }

    [Id(3)]
    public ApprovalState State { get; init; } = ApprovalState.Pending;

    [Id(4)]
    public string RequestedAction { get; init; } = string.Empty;

    [Id(5)]
    public string RequestedBy { get; init; } = string.Empty;

    [Id(6)]
    public DateTimeOffset RequestedAt { get; init; }

    [Id(7)]
    public DateTimeOffset? ResolvedAt { get; init; }
}

[GenerateSerializer]
public sealed record ArtifactDescriptor
{
    [Id(0)]
    public ArtifactId Id { get; init; }

    [Id(1)]
    public SessionId SessionId { get; init; }

    [Id(2)]
    public AgentProfileId? AgentProfileId { get; init; }

    [Id(3)]
    public string Name { get; init; } = string.Empty;

    [Id(4)]
    public ArtifactKind Kind { get; init; }

    [Id(5)]
    public string RelativePath { get; init; } = string.Empty;

    [Id(6)]
    public DateTimeOffset CreatedAt { get; init; }
}

[GenerateSerializer]
public sealed record TelemetryRecord
{
    [Id(0)]
    public TelemetryRecordId Id { get; init; }

    [Id(1)]
    public SessionId SessionId { get; init; }

    [Id(2)]
    public TelemetrySignalKind Kind { get; init; }

    [Id(3)]
    public string Name { get; init; } = string.Empty;

    [Id(4)]
    public string Summary { get; init; } = string.Empty;

    [Id(5)]
    public DateTimeOffset RecordedAt { get; init; }
}

[GenerateSerializer]
public sealed record EvaluationRecord
{
    [Id(0)]
    public EvaluationId Id { get; init; }

    [Id(1)]
    public SessionId SessionId { get; init; }

    [Id(2)]
    public ArtifactId? ArtifactId { get; init; }

    [Id(3)]
    public EvaluationMetricKind Metric { get; init; }

    [Id(4)]
    public decimal Score { get; init; }

    [Id(5)]
    public EvaluationOutcome Outcome { get; init; } = EvaluationOutcome.NeedsReview;

    [Id(6)]
    public string Summary { get; init; } = string.Empty;

    [Id(7)]
    public DateTimeOffset EvaluatedAt { get; init; }
}
