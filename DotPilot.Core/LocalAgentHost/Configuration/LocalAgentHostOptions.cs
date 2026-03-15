namespace DotPilot.Core.LocalAgentHost;

public sealed class LocalAgentHostOptions
{
    public string? StorageBasePath { get; init; }

    public string GrainStateDirectory { get; init; } = "orleans/grain-state";

    public string ClusterId { get; init; } = LocalAgentHostNames.DefaultClusterId;

    public string ServiceId { get; init; } = LocalAgentHostNames.DefaultServiceId;

    public int SiloPort { get; init; } = LocalAgentHostNames.DefaultSiloPort;

    public int GatewayPort { get; init; } = LocalAgentHostNames.DefaultGatewayPort;
}
