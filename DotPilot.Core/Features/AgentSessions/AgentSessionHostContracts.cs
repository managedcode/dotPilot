using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.AgentSessions;

public interface ISessionGrain : IGrainWithStringKey
{
    ValueTask<SessionDescriptor?> GetAsync();

    ValueTask<SessionDescriptor> UpsertAsync(SessionDescriptor session);
}

public interface IAgentProfileGrain : IGrainWithStringKey
{
    ValueTask<AgentProfileDescriptor?> GetAsync();

    ValueTask<AgentProfileDescriptor> UpsertAsync(AgentProfileDescriptor agentProfile);
}
