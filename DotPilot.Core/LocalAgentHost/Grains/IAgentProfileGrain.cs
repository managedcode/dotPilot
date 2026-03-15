using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.LocalAgentHost;

public interface IAgentProfileGrain : IGrainWithStringKey
{
    ValueTask<AgentProfileDescriptor?> GetAsync();

    ValueTask<AgentProfileDescriptor> UpsertAsync(AgentProfileDescriptor agentProfile);
}
