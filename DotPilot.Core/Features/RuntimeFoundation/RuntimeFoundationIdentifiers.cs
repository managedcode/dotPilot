using System.Globalization;

namespace DotPilot.Core.Features.RuntimeFoundation;

public readonly record struct WorkspaceId(Guid Value)
{
    public static WorkspaceId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}

public readonly record struct AgentProfileId(Guid Value)
{
    public static AgentProfileId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}

public readonly record struct FleetId(Guid Value)
{
    public static FleetId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}

public readonly record struct ProviderId(Guid Value)
{
    public static ProviderId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}

public readonly record struct ModelRuntimeId(Guid Value)
{
    public static ModelRuntimeId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N", CultureInfo.InvariantCulture);
}
