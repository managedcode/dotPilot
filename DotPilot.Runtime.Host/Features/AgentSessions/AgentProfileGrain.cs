using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Runtime.Host.Features.AgentSessions;

public sealed class AgentProfileGrain(
    [PersistentState(AgentSessionHostNames.AgentStateName, AgentSessionHostNames.GrainStorageProviderName)]
    IPersistentState<AgentProfileDescriptor> agentState) : Grain, IAgentProfileGrain
{
    public ValueTask<AgentProfileDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(agentState.RecordExists ? agentState.State : null);
    }

    public async ValueTask<AgentProfileDescriptor> UpsertAsync(AgentProfileDescriptor agentProfile)
    {
        EnsureMatchingKey(agentProfile.Id.ToString(), this.GetPrimaryKeyString(), AgentSessionHostNames.AgentGrainName);
        agentState.State = agentProfile;
        await agentState.WriteStateAsync();
        return agentState.State;
    }

    private static void EnsureMatchingKey(string expectedKey, string actualKey, string grainName)
    {
        if (!string.Equals(expectedKey, actualKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Descriptor id does not match the grain primary key for {grainName}.");
        }
    }
}
