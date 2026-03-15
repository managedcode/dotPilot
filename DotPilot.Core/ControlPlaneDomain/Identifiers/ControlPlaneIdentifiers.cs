using System.Globalization;

namespace DotPilot.Core.ControlPlaneDomain;

[GenerateSerializer]
public readonly record struct WorkspaceId([property: Id(0)] Guid Value)
{
    public static WorkspaceId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct AgentProfileId([property: Id(0)] Guid Value)
{
    public static AgentProfileId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct SessionId([property: Id(0)] Guid Value)
{
    public static SessionId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct FleetId([property: Id(0)] Guid Value)
{
    public static FleetId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct PolicyId([property: Id(0)] Guid Value)
{
    public static PolicyId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct ProviderId([property: Id(0)] Guid Value)
{
    public static ProviderId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct ModelRuntimeId([property: Id(0)] Guid Value)
{
    public static ModelRuntimeId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct ToolCapabilityId([property: Id(0)] Guid Value)
{
    public static ToolCapabilityId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct ApprovalId([property: Id(0)] Guid Value)
{
    public static ApprovalId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct ArtifactId([property: Id(0)] Guid Value)
{
    public static ArtifactId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct TelemetryRecordId([property: Id(0)] Guid Value)
{
    public static TelemetryRecordId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

[GenerateSerializer]
public readonly record struct EvaluationId([property: Id(0)] Guid Value)
{
    public static EvaluationId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

static file class ControlPlaneIdentifier
{
    public static Guid NewValue() => Guid.CreateVersion7();

    public static string Format(Guid value) => value.ToString("N", CultureInfo.InvariantCulture);
}
