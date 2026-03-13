using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal sealed class EmbeddedRuntimeHostCatalog(EmbeddedRuntimeHostOptions options) : IEmbeddedRuntimeHostCatalog
{
    private int _state = (int)EmbeddedRuntimeHostState.Stopped;

    public EmbeddedRuntimeHostSnapshot GetSnapshot()
    {
        return new(
            (EmbeddedRuntimeHostState)Volatile.Read(ref _state),
            EmbeddedRuntimeClusteringMode.Localhost,
            EmbeddedRuntimeStorageMode.InMemory,
            EmbeddedRuntimeStorageMode.InMemory,
            options.ClusterId,
            options.ServiceId,
            options.SiloPort,
            options.GatewayPort,
            CreateGrains());
    }

    public void SetState(EmbeddedRuntimeHostState state)
    {
        Volatile.Write(ref _state, (int)state);
    }

    private static IReadOnlyList<EmbeddedRuntimeGrainDescriptor> CreateGrains()
    {
        return
        [
            new(EmbeddedRuntimeHostNames.SessionGrainName, EmbeddedRuntimeHostNames.SessionGrainSummary),
            new(EmbeddedRuntimeHostNames.WorkspaceGrainName, EmbeddedRuntimeHostNames.WorkspaceGrainSummary),
            new(EmbeddedRuntimeHostNames.FleetGrainName, EmbeddedRuntimeHostNames.FleetGrainSummary),
            new(EmbeddedRuntimeHostNames.PolicyGrainName, EmbeddedRuntimeHostNames.PolicyGrainSummary),
            new(EmbeddedRuntimeHostNames.ArtifactGrainName, EmbeddedRuntimeHostNames.ArtifactGrainSummary),
        ];
    }
}
