using System.Globalization;

namespace DotPilot.Core.Features.ControlPlaneDomain;

public readonly record struct WorkspaceId(Guid Value)
{
    public static WorkspaceId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct AgentProfileId(Guid Value)
{
    public static AgentProfileId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct FleetId(Guid Value)
{
    public static FleetId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct ProviderId(Guid Value)
{
    public static ProviderId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct ModelRuntimeId(Guid Value)
{
    public static ModelRuntimeId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct ToolCapabilityId(Guid Value)
{
    public static ToolCapabilityId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct ApprovalId(Guid Value)
{
    public static ApprovalId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct TelemetryRecordId(Guid Value)
{
    public static TelemetryRecordId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

public readonly record struct EvaluationId(Guid Value)
{
    public static EvaluationId New() => new(ControlPlaneIdentifier.NewValue());

    public override string ToString() => ControlPlaneIdentifier.Format(Value);
}

static file class ControlPlaneIdentifier
{
    public static Guid NewValue() => Guid.CreateVersion7();

    public static string Format(Guid value) => value.ToString("N", CultureInfo.InvariantCulture);
}
