using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication;

namespace DotPilot.Core.Workspace;

internal sealed class AgentWorkspaceState(IAgentSessionService agentSessionService) : IAgentWorkspaceState
{
    public ValueTask<Result<AgentWorkspaceSnapshot>> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        return agentSessionService.GetWorkspaceAsync(cancellationToken);
    }

    public ValueTask<Result<AgentWorkspaceSnapshot>> RefreshWorkspaceAsync(CancellationToken cancellationToken)
    {
        return agentSessionService.RefreshWorkspaceAsync(cancellationToken);
    }

    public ValueTask<Result<SessionTranscriptSnapshot>> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        return agentSessionService.GetSessionAsync(sessionId, cancellationToken);
    }

    public ValueTask<Result<AgentProfileSummary>> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        return agentSessionService.CreateAgentAsync(command, cancellationToken);
    }

    public ValueTask<Result<AgentProfileSummary>> UpdateAgentAsync(
        UpdateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        return agentSessionService.UpdateAgentAsync(command, cancellationToken);
    }

    public ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        return agentSessionService.CreateSessionAsync(command, cancellationToken);
    }

    public ValueTask<Result<ProviderStatusDescriptor>> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        return agentSessionService.UpdateProviderAsync(command, cancellationToken);
    }

    public IAsyncEnumerable<Result<SessionStreamEntry>> SendMessageAsync(
        SendSessionMessageCommand command,
        CancellationToken cancellationToken)
    {
        return agentSessionService.SendMessageAsync(command, cancellationToken);
    }
}
