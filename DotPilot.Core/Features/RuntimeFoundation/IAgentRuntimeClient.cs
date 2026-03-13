namespace DotPilot.Core.Features.RuntimeFoundation;

public interface IAgentRuntimeClient
{
    ValueTask<AgentTurnResult> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken);
}
