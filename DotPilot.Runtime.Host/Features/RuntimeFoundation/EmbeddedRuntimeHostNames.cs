namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal static class EmbeddedRuntimeHostNames
{
    public const string DefaultClusterId = "dotpilot-local";
    public const string DefaultServiceId = "dotpilot-desktop";
    public const int DefaultSiloPort = 11_111;
    public const int DefaultGatewayPort = 30_000;
    public const string GrainStorageProviderName = "runtime-foundation-memory";
    public const string ClientSourceName = "Client";
    public const string ClientSourceMethodName = "Invoke";
    public const string SessionStateName = "session";
    public const string WorkspaceStateName = "workspace";
    public const string FleetStateName = "fleet";
    public const string PolicyStateName = "policy";
    public const string ArtifactStateName = "artifact";
    public const string SessionGrainName = "Session";
    public const string WorkspaceGrainName = "Workspace";
    public const string FleetGrainName = "Fleet";
    public const string PolicyGrainName = "Policy";
    public const string ArtifactGrainName = "Artifact";
    public const string SessionGrainSummary = "Stores local session state in the embedded runtime host.";
    public const string WorkspaceGrainSummary = "Stores local workspace descriptors for the embedded runtime host.";
    public const string FleetGrainSummary = "Stores participating agent fleet descriptors for local orchestration.";
    public const string PolicyGrainSummary = "Stores local approval and execution policy defaults.";
    public const string ArtifactGrainSummary = "Stores artifact metadata for the local embedded runtime.";
    public const string MismatchedPrimaryKeyPrefix = "Descriptor id does not match the grain primary key for ";
}
