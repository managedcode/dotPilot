using DotPilot.Core.Features.ControlPlaneDomain;
using ManagedCode.Communication;

namespace DotPilot.Core.Features.RuntimeFoundation;

public interface IAgentRuntimeClient
{
    ValueTask<Result<AgentTurnResult>> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken);

    ValueTask<Result<AgentTurnResult>> ResumeAsync(AgentTurnResumeRequest request, CancellationToken cancellationToken);

    ValueTask<Result<RuntimeSessionArchive>> GetSessionArchiveAsync(SessionId sessionId, CancellationToken cancellationToken);
}
