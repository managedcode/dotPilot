namespace DotPilot.Runtime.Host.Features.AgentSessions;

public sealed class AgentSessionHostOptions
{
    public string ClusterId { get; init; } = AgentSessionHostNames.DefaultClusterId;

    public string ServiceId { get; init; } = AgentSessionHostNames.DefaultServiceId;

    public int SiloPort { get; init; } = AgentSessionHostNames.DefaultSiloPort;

    public int GatewayPort { get; init; } = AgentSessionHostNames.DefaultGatewayPort;
}

