using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class PolicyGrain(
    [PersistentState(EmbeddedRuntimeHostNames.PolicyStateName, EmbeddedRuntimeHostNames.GrainStorageProviderName)]
    IPersistentState<PolicyDescriptor> policyState) : Grain, IPolicyGrain
{
    public ValueTask<PolicyDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(policyState.RecordExists ? policyState.State : null);
    }

    public async ValueTask<PolicyDescriptor> UpsertAsync(PolicyDescriptor policy)
    {
        EmbeddedRuntimeGrainGuards.EnsureMatchingKey(policy.Id.ToString(), this.GetPrimaryKeyString(), EmbeddedRuntimeHostNames.PolicyGrainName);
        policyState.State = policy;
        await policyState.WriteStateAsync();
        return policyState.State;
    }
}
