using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class FleetGrain(
    [PersistentState(EmbeddedRuntimeHostNames.FleetStateName, EmbeddedRuntimeHostNames.GrainStorageProviderName)]
    IPersistentState<FleetDescriptor> fleetState) : Grain, IFleetGrain
{
    public ValueTask<FleetDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(fleetState.RecordExists ? fleetState.State : null);
    }

    public async ValueTask<FleetDescriptor> UpsertAsync(FleetDescriptor fleet)
    {
        EmbeddedRuntimeGrainGuards.EnsureMatchingKey(fleet.Id.ToString(), this.GetPrimaryKeyString(), EmbeddedRuntimeHostNames.FleetGrainName);
        fleetState.State = fleet;
        await fleetState.WriteStateAsync();
        return fleetState.State;
    }
}
