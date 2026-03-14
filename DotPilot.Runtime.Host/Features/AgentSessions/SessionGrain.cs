using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Runtime.Host.Features.AgentSessions;

public sealed class SessionGrain(
    [PersistentState(AgentSessionHostNames.SessionStateName, AgentSessionHostNames.GrainStorageProviderName)]
    IPersistentState<SessionDescriptor> sessionState) : Grain, ISessionGrain
{
    public ValueTask<SessionDescriptor?> GetAsync()
    {
        return ValueTask.FromResult(sessionState.RecordExists ? sessionState.State : null);
    }

    public async ValueTask<SessionDescriptor> UpsertAsync(SessionDescriptor session)
    {
        EnsureMatchingKey(session.Id.ToString(), this.GetPrimaryKeyString(), AgentSessionHostNames.SessionGrainName);
        sessionState.State = session;
        await sessionState.WriteStateAsync();
        return sessionState.State;
    }

    private static void EnsureMatchingKey(string expectedKey, string actualKey, string grainName)
    {
        if (!string.Equals(expectedKey, actualKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Descriptor id does not match the grain primary key for {grainName}.");
        }
    }
}

