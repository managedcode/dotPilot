using ManagedCode.Communication;

namespace DotPilot.Core.Features.RuntimeFoundation;

public interface IAgentRuntimeClient
{
    ValueTask<Result<AgentTurnResult>> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken);
}
