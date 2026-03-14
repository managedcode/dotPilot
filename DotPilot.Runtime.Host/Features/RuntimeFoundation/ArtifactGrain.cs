using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class ArtifactGrain(
    [PersistentState(EmbeddedRuntimeHostNames.ArtifactStateName, EmbeddedRuntimeHostNames.GrainStorageProviderName)]
    IPersistentState<ArtifactDescriptor> artifactState) : Grain, IArtifactGrain
{
    public ValueTask<ArtifactDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(artifactState.RecordExists ? artifactState.State : null);
    }

    public async ValueTask<ArtifactDescriptor> UpsertAsync(ArtifactDescriptor artifact)
    {
        EmbeddedRuntimeGrainGuards.EnsureMatchingKey(artifact.Id.ToString(), this.GetPrimaryKeyString(), EmbeddedRuntimeHostNames.ArtifactGrainName);
        artifactState.State = artifact;
        await artifactState.WriteStateAsync();
        return artifactState.State;
    }
}
