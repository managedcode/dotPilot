using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.AgentSessions;

public interface IAgentSessionService
{
    ValueTask<AgentWorkspaceSnapshot> GetWorkspaceAsync(CancellationToken cancellationToken);

    ValueTask<SessionTranscriptSnapshot?> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken);

    ValueTask<AgentProfileSummary> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken);

    ValueTask<SessionTranscriptSnapshot> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken);

    ValueTask<ProviderStatusDescriptor> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken);

    IAsyncEnumerable<SessionStreamEntry> SendMessageAsync(
        SendSessionMessageCommand command,
        CancellationToken cancellationToken);
}
