using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication;

namespace DotPilot.Core.ChatSessions.Interfaces;

public interface IAgentSessionService
{
    ValueTask<Result<AgentWorkspaceSnapshot>> GetWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<Result<AgentWorkspaceSnapshot>> RefreshWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<Result<SessionTranscriptSnapshot>> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken);

    ValueTask<Result<AgentProfileSummary>> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<ProviderStatusDescriptor>> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken);

    IAsyncEnumerable<Result<SessionStreamEntry>> SendMessageAsync(
        SendSessionMessageCommand command,
        CancellationToken cancellationToken);
}
