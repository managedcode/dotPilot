namespace DotPilot.Core.Features.ControlPlaneDomain;

public sealed record WorkspaceDescriptor
{
    public WorkspaceId Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string RootPath { get; init; } = string.Empty;

    public string BranchName { get; init; } = string.Empty;
}

public sealed record AgentProfileDescriptor
{
    public AgentProfileId Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public AgentRoleKind Role { get; init; }

    public ProviderId? ProviderId { get; init; }

    public ModelRuntimeId? ModelRuntimeId { get; init; }

    public IReadOnlyList<ToolCapabilityId> ToolCapabilityIds { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed record FleetDescriptor
{
    public FleetId Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public FleetExecutionMode ExecutionMode { get; init; } = FleetExecutionMode.SingleAgent;

    public IReadOnlyList<AgentProfileId> AgentProfileIds { get; init; } = [];
}
