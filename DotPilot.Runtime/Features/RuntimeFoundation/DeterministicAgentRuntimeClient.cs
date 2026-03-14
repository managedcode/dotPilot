using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;
using ManagedCode.Communication;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class DeterministicAgentRuntimeClient : IAgentRuntimeClient
{
    private readonly DeterministicAgentTurnEngine _engine;

    public DeterministicAgentRuntimeClient()
        : this(TimeProvider.System)
    {
    }

    internal DeterministicAgentRuntimeClient(TimeProvider timeProvider)
    {
        _engine = new DeterministicAgentTurnEngine(timeProvider);
    }

    public ValueTask<Result<AgentTurnResult>> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_engine.Execute(request));
    }

    public ValueTask<Result<AgentTurnResult>> ResumeAsync(AgentTurnResumeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = request;
        return ValueTask.FromResult(Result<AgentTurnResult>.Fail(DotPilot.Core.Features.RuntimeCommunication.RuntimeCommunicationProblems.OrchestrationUnavailable()));
    }

    public ValueTask<Result<RuntimeSessionArchive>> GetSessionArchiveAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<RuntimeSessionArchive>.Fail(DotPilot.Core.Features.RuntimeCommunication.RuntimeCommunicationProblems.SessionArchiveMissing(sessionId)));
    }
}
