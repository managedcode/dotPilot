namespace DotPilot.Runtime.Host.Features.AgentSessions;

internal static class AgentSessionHostNames
{
    public const string DefaultClusterId = "dotpilot-local";
    public const string DefaultServiceId = "dotpilot-desktop";
    public const int DefaultSiloPort = 11_111;
    public const int DefaultGatewayPort = 30_000;
    public const string GrainStorageProviderName = "agent-sessions-memory";
    public const string SessionStateName = "session";
    public const string AgentStateName = "agent";
    public const string SessionGrainName = "Session";
    public const string AgentGrainName = "Agent";
}

