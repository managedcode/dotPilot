using ManagedCode.Communication;

namespace DotPilot.Core.ChatSessions.Interfaces;

public interface IAgentSessionService
{
    ValueTask<Result<AgentWorkspaceSnapshot>> GetWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<Result<AgentWorkspaceSnapshot>> RefreshWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<Result<AgentWorkspaceSnapshot>> ResetWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<Result<SessionTranscriptSnapshot>> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken);

    ValueTask<Result<AgentProfileSummary>> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<AgentProfileSummary>> UpdateAgentAsync(
        UpdateAgentProfileCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<AgentWorkspaceSnapshot>> CloseSessionAsync(
        CloseSessionCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<ProviderStatusDescriptor>> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken);

    ValueTask<Result<ProviderStatusDescriptor>> SetLocalModelPathAsync(
        SetLocalModelPathCommand command,
        CancellationToken cancellationToken);

    IAsyncEnumerable<Result<SessionStreamEntry>> SendMessageAsync(
        SendSessionMessageCommand command,
        CancellationToken cancellationToken);
}
