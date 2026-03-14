namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class EmbeddedRuntimeHostOptions
{
    public string ClusterId { get; init; } = EmbeddedRuntimeHostNames.DefaultClusterId;

    public string ServiceId { get; init; } = EmbeddedRuntimeHostNames.DefaultServiceId;

    public int SiloPort { get; init; } = EmbeddedRuntimeHostNames.DefaultSiloPort;

    public int GatewayPort { get; init; } = EmbeddedRuntimeHostNames.DefaultGatewayPort;
}
