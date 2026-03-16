namespace DotPilot.Core.ControlPlaneDomain;

[GenerateSerializer]
public sealed record WorkspaceDescriptor
{
    [Id(0)]
    public WorkspaceId Id { get; init; }

    [Id(1)]
    public string Name { get; init; } = string.Empty;

    [Id(2)]
    public string RootPath { get; init; } = string.Empty;

    [Id(3)]
    public string BranchName { get; init; } = string.Empty;
}

[GenerateSerializer]
public sealed record AgentProfileDescriptor
{
    [Id(0)]
    public AgentProfileId Id { get; init; }

    [Id(1)]
    public string Name { get; init; } = string.Empty;

    [Id(2)]
    public ProviderId? ProviderId { get; init; }

    [Id(3)]
    public ModelRuntimeId? ModelRuntimeId { get; init; }

    [Id(4)]
    public ToolCapabilityId[] ToolCapabilityIds { get; init; } = [];

    [Id(5)]
    public string[] Tags { get; init; } = [];
}

[GenerateSerializer]
public sealed record FleetDescriptor
{
    [Id(0)]
    public FleetId Id { get; init; }

    [Id(1)]
    public string Name { get; init; } = string.Empty;

    [Id(2)]
    public FleetExecutionMode ExecutionMode { get; init; } = FleetExecutionMode.SingleAgent;

    [Id(3)]
    public AgentProfileId[] AgentProfileIds { get; init; } = [];
}
