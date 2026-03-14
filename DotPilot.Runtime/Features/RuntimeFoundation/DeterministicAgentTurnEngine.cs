using System.Security.Cryptography;
using System.Text;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeCommunication;
using DotPilot.Core.Features.RuntimeFoundation;
using ManagedCode.Communication;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

internal sealed class DeterministicAgentTurnEngine(TimeProvider timeProvider)
{
    private const string ApprovalKeyword = "approval";
    private const string PlanSummary =
        "Prepared a local-first runtime plan with isolated orchestration, storage, and policy boundaries.";
    private const string ExecuteSummary =
        "Completed the deterministic runtime flow and produced the expected local session artifacts.";
    private const string PendingApprovalSummary =
        "Paused the deterministic runtime flow because the prompt requested an approval checkpoint.";
    private const string ResumedExecutionSummary =
        "Resumed the persisted runtime flow after approval and completed the pending execution step.";
    private const string RejectedExecutionSummary =
        "Stopped the persisted runtime flow because the approval checkpoint was rejected.";
    private const string ReviewSummary =
        "Reviewed the runtime output and prepared the local session summary for the next operator action.";
    private const string PlanArtifact = "runtime-foundation.plan.md";
    private const string ExecuteArtifact = "runtime-foundation.snapshot.json";
    private const string ReviewArtifact = "runtime-foundation.review.md";
    private const string ArtifactIdentityDelimiter = "|";

    public Result<AgentTurnResult> Execute(AgentTurnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.InvalidPrompt());
        }

        if (request.ProviderStatus is not ProviderConnectionStatus.Available)
        {
            return Result<AgentTurnResult>.Fail(
                RuntimeCommunicationProblems.ProviderUnavailable(
                    request.ProviderStatus,
                    ProviderToolchainNames.DeterministicClientDisplayName));
        }

        return request.Mode switch
        {
            AgentExecutionMode.Plan => Result<AgentTurnResult>.Succeed(
                CreateResult(
                    request.SessionId,
                    request.AgentProfileId,
                    PlanSummary,
                    SessionPhase.Plan,
                    ApprovalState.NotRequired,
                    PlanArtifact,
                    ArtifactKind.Plan)),
            AgentExecutionMode.Execute when RequiresApproval(request.Prompt) => Result<AgentTurnResult>.Succeed(
                CreateResult(
                    request.SessionId,
                    request.AgentProfileId,
                    PendingApprovalSummary,
                    SessionPhase.Paused,
                    ApprovalState.Pending,
                    ExecuteArtifact,
                    ArtifactKind.Snapshot)),
            AgentExecutionMode.Execute => Result<AgentTurnResult>.Succeed(
                CreateResult(
                    request.SessionId,
                    request.AgentProfileId,
                    ExecuteSummary,
                    SessionPhase.Execute,
                    ApprovalState.NotRequired,
                    ExecuteArtifact,
                    ArtifactKind.Snapshot)),
            AgentExecutionMode.Review => Result<AgentTurnResult>.Succeed(
                CreateResult(
                    request.SessionId,
                    request.AgentProfileId,
                    ReviewSummary,
                    SessionPhase.Review,
                    ApprovalState.Approved,
                    ReviewArtifact,
                    ArtifactKind.Report)),
            _ => Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.OrchestrationUnavailable()),
        };
    }

    public AgentTurnResult Resume(AgentTurnRequest request, AgentTurnResumeRequest resumeRequest)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resumeRequest);

        return resumeRequest.ApprovalState switch
        {
            ApprovalState.Approved => CreateResult(
                request.SessionId,
                request.AgentProfileId,
                ResumedExecutionSummary,
                SessionPhase.Execute,
                ApprovalState.Approved,
                ExecuteArtifact,
                ArtifactKind.Snapshot),
            ApprovalState.Rejected => CreateResult(
                request.SessionId,
                request.AgentProfileId,
                string.IsNullOrWhiteSpace(resumeRequest.Summary) ? RejectedExecutionSummary : resumeRequest.Summary,
                SessionPhase.Failed,
                ApprovalState.Rejected,
                ReviewArtifact,
                ArtifactKind.Report),
            _ => CreateResult(
                request.SessionId,
                request.AgentProfileId,
                PendingApprovalSummary,
                SessionPhase.Paused,
                ApprovalState.Pending,
                ExecuteArtifact,
                ArtifactKind.Snapshot),
        };
    }

    public static bool RequiresApproval(string prompt)
    {
        return prompt.Contains(ApprovalKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private AgentTurnResult CreateResult(
        SessionId sessionId,
        AgentProfileId agentProfileId,
        string summary,
        SessionPhase nextPhase,
        ApprovalState approvalState,
        string artifactName,
        ArtifactKind artifactKind)
    {
        return new AgentTurnResult(
            summary,
            nextPhase,
            approvalState,
            [CreateArtifact(sessionId, agentProfileId, artifactName, artifactKind)]);
    }

    private ArtifactDescriptor CreateArtifact(
        SessionId sessionId,
        AgentProfileId agentProfileId,
        string artifactName,
        ArtifactKind artifactKind)
    {
        return new ArtifactDescriptor
        {
            Id = new ArtifactId(CreateDeterministicGuid(sessionId, artifactName, artifactKind)),
            SessionId = sessionId,
            AgentProfileId = agentProfileId,
            Name = artifactName,
            Kind = artifactKind,
            RelativePath = artifactName,
            CreatedAt = timeProvider.GetUtcNow(),
        };
    }

    private static Guid CreateDeterministicGuid(SessionId sessionId, string artifactName, ArtifactKind artifactKind)
    {
        var rawIdentity = string.Join(
            ArtifactIdentityDelimiter,
            sessionId.ToString(),
            artifactName,
            artifactKind.ToString());
        Span<byte> bytes = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(rawIdentity), bytes);
        return new Guid(bytes[..16]);
    }
}
