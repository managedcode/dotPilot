using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.RuntimeFoundation;

public enum EmbeddedRuntimeHostState
{
    Stopped,
    Starting,
    Running,
}

public enum EmbeddedRuntimeClusteringMode
{
    Localhost,
}

public enum EmbeddedRuntimeStorageMode
{
    InMemory,
}

public sealed record EmbeddedRuntimeGrainDescriptor(
    string Name,
    string Summary);

public sealed record EmbeddedRuntimeHostSnapshot(
    EmbeddedRuntimeHostState State,
    EmbeddedRuntimeClusteringMode ClusteringMode,
    EmbeddedRuntimeStorageMode GrainStorageMode,
    EmbeddedRuntimeStorageMode ReminderStorageMode,
    string ClusterId,
    string ServiceId,
    int SiloPort,
    int GatewayPort,
    IReadOnlyList<EmbeddedRuntimeGrainDescriptor> Grains);

public interface IEmbeddedRuntimeHostCatalog
{
    EmbeddedRuntimeHostSnapshot GetSnapshot();
}

public interface ISessionGrain : IGrainWithStringKey
{
    ValueTask<SessionDescriptor?> GetAsync();

    ValueTask<SessionDescriptor> UpsertAsync(SessionDescriptor session);
}

public interface IWorkspaceGrain : IGrainWithStringKey
{
    ValueTask<WorkspaceDescriptor?> GetAsync();

    ValueTask<WorkspaceDescriptor> UpsertAsync(WorkspaceDescriptor workspace);
}

public interface IFleetGrain : IGrainWithStringKey
{
    ValueTask<FleetDescriptor?> GetAsync();

    ValueTask<FleetDescriptor> UpsertAsync(FleetDescriptor fleet);
}

public interface IPolicyGrain : IGrainWithStringKey
{
    ValueTask<PolicyDescriptor?> GetAsync();

    ValueTask<PolicyDescriptor> UpsertAsync(PolicyDescriptor policy);
}

public interface IArtifactGrain : IGrainWithStringKey
{
    ValueTask<ArtifactDescriptor?> GetAsync();

    ValueTask<ArtifactDescriptor> UpsertAsync(ArtifactDescriptor artifact);
}
