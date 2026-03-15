using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.LocalAgentHost;

public interface ISessionGrain : IGrainWithStringKey
{
    ValueTask<SessionDescriptor?> GetAsync();

    ValueTask<SessionDescriptor> UpsertAsync(SessionDescriptor session);
}
