namespace DotPilot.Core.Features.ControlPlaneDomain;

public sealed record SessionDescriptor
{
    public SessionId Id { get; init; }

    public WorkspaceId WorkspaceId { get; init; }

    public string Title { get; init; } = string.Empty;

    public SessionPhase Phase { get; init; } = SessionPhase.Plan;

    public ApprovalState ApprovalState { get; init; } = ApprovalState.NotRequired;

    public FleetId? FleetId { get; init; }

    public IReadOnlyList<AgentProfileId> AgentProfileIds { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record SessionApprovalRecord
{
    public ApprovalId Id { get; init; }

    public SessionId SessionId { get; init; }

    public ApprovalScope Scope { get; init; }

    public ApprovalState State { get; init; } = ApprovalState.Pending;

    public string RequestedAction { get; init; } = string.Empty;

    public string RequestedBy { get; init; } = string.Empty;

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}

public sealed record ArtifactDescriptor
{
    public ArtifactId Id { get; init; }

    public SessionId SessionId { get; init; }

    public AgentProfileId? AgentProfileId { get; init; }

    public string Name { get; init; } = string.Empty;

    public ArtifactKind Kind { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record TelemetryRecord
{
    public TelemetryRecordId Id { get; init; }

    public SessionId SessionId { get; init; }

    public TelemetrySignalKind Kind { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; }
}

public sealed record EvaluationRecord
{
    public EvaluationId Id { get; init; }

    public SessionId SessionId { get; init; }

    public ArtifactId? ArtifactId { get; init; }

    public EvaluationMetricKind Metric { get; init; }

    public decimal Score { get; init; }

    public EvaluationOutcome Outcome { get; init; } = EvaluationOutcome.NeedsReview;

    public string Summary { get; init; } = string.Empty;

    public DateTimeOffset EvaluatedAt { get; init; }
}
