using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeCommunication;
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
        return ValueTask.FromResult(NormalizeArtifacts(_engine.Execute(request)));
    }

    public ValueTask<Result<AgentTurnResult>> ResumeAsync(AgentTurnResumeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = request;
        return ValueTask.FromResult(Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.OrchestrationUnavailable()));
    }

    public ValueTask<Result<RuntimeSessionArchive>> GetSessionArchiveAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Result<RuntimeSessionArchive>.Fail(RuntimeCommunicationProblems.SessionArchiveMissing(sessionId)));
    }

    private static Result<AgentTurnResult> NormalizeArtifacts(Result<AgentTurnResult> result)
    {
        if (result.IsFailed || result.Value is null)
        {
            return result;
        }

        var outcome = result.Value;
        var normalizedArtifacts = outcome.ProducedArtifacts
            .Select(artifact => artifact with { CreatedAt = RuntimeFoundationDeterministicIdentity.ArtifactCreatedAt })
            .ToArray();

        return Result<AgentTurnResult>.Succeed(
            new AgentTurnResult(
                outcome.Summary,
                outcome.NextPhase,
                outcome.ApprovalState,
                normalizedArtifacts));
    }
}
