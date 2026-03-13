using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class WorkspaceGrain(
    [PersistentState(EmbeddedRuntimeHostNames.WorkspaceStateName, EmbeddedRuntimeHostNames.GrainStorageProviderName)]
    IPersistentState<WorkspaceDescriptor> workspaceState) : Grain, IWorkspaceGrain
{
    public ValueTask<WorkspaceDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(workspaceState.RecordExists ? workspaceState.State : null);
    }

    public async ValueTask<WorkspaceDescriptor> UpsertAsync(WorkspaceDescriptor workspace)
    {
        EmbeddedRuntimeGrainGuards.EnsureMatchingKey(workspace.Id.ToString(), this.GetPrimaryKeyString(), EmbeddedRuntimeHostNames.WorkspaceGrainName);
        workspaceState.State = workspace;
        await workspaceState.WriteStateAsync();
        return workspaceState.State;
    }
}
