using System.Diagnostics;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class DeterministicAgentRuntimeClient : IAgentRuntimeClient
{
    private const string ApprovalKeyword = "approval";
    private const string PlanSummary =
        "Planned the runtime foundation flow with contracts first, then communication, host lifecycle, and orchestration.";
    private const string ExecuteSummary =
        "Executed the provider-independent runtime path with the deterministic client and produced the expected artifact manifest.";
    private const string PendingApprovalSummary =
        "The deterministic runtime path paused because the prompt explicitly requested an approval checkpoint.";
    private const string ReviewSummary =
        "Reviewed the runtime foundation output and confirmed the slice is ready for the next implementation branch.";
    private const string PlanArtifact = "runtime-foundation.plan.md";
    private const string ExecuteArtifact = "runtime-foundation.snapshot.json";
    private const string ReviewArtifact = "runtime-foundation.review.md";

    public ValueTask<AgentTurnResult> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Prompt);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(request.Mode switch
        {
            AgentExecutionMode.Plan => new AgentTurnResult(
                PlanSummary,
                SessionPhase.Plan,
                ApprovalState.NotRequired,
                [PlanArtifact]),
            AgentExecutionMode.Execute when RequiresApproval(request.Prompt) => new AgentTurnResult(
                PendingApprovalSummary,
                SessionPhase.Paused,
                ApprovalState.Pending,
                [ExecuteArtifact]),
            AgentExecutionMode.Execute => new AgentTurnResult(
                ExecuteSummary,
                SessionPhase.Execute,
                ApprovalState.NotRequired,
                [ExecuteArtifact]),
            AgentExecutionMode.Review => new AgentTurnResult(
                ReviewSummary,
                SessionPhase.Review,
                ApprovalState.Approved,
                [ReviewArtifact]),
            _ => throw new UnreachableException(),
        });
    }

    private static bool RequiresApproval(string prompt)
    {
        return prompt.Contains(ApprovalKeyword, StringComparison.OrdinalIgnoreCase);
    }
}
