using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public sealed class SessionGrain(
    [PersistentState(EmbeddedRuntimeHostNames.SessionStateName, EmbeddedRuntimeHostNames.GrainStorageProviderName)]
    IPersistentState<SessionDescriptor> sessionState) : Grain, ISessionGrain
{
    public ValueTask<SessionDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(sessionState.RecordExists ? sessionState.State : null);
    }

    public async ValueTask<SessionDescriptor> UpsertAsync(SessionDescriptor session)
    {
        EmbeddedRuntimeGrainGuards.EnsureMatchingKey(session.Id.ToString(), this.GetPrimaryKeyString(), EmbeddedRuntimeHostNames.SessionGrainName);
        sessionState.State = session;
        await sessionState.WriteStateAsync();
        return sessionState.State;
    }
}
