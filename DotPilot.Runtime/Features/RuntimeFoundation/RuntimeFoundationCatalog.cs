using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;
using DotPilot.Runtime.Features.ToolchainCenter;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class RuntimeFoundationCatalog : IRuntimeFoundationCatalog
{
    private const string EpicSummary =
        "The embedded runtime stays local-first by isolating contracts, host wiring, orchestration, policy, and durable session archives away from the Uno presentation layer.";
    private const string DeterministicProbePrompt =
        "Summarize the runtime foundation readiness for a local-first session that may require approval.";
    private const string DeterministicClientStatusSummary = "Always available for in-repo and CI validation.";
    private const string DomainModelName = "Domain contracts";
    private const string DomainModelSummary =
        "Typed identifiers and durable agent, session, fleet, provider, and runtime contracts live outside the Uno app.";
    private const string CommunicationName = "Communication contracts";
    private const string CommunicationSummary =
        "Public result and problem boundaries are isolated so later provider and orchestration slices share one contract language.";
    private const string HostName = "Embedded host";
    private const string HostSummary =
        "The Orleans host integration point is sequenced behind dedicated runtime contracts instead of being baked into page code.";
    private const string OrchestrationName = "Orchestration runtime";
    private const string OrchestrationSummary =
        "Agent Framework orchestrates local runs, approvals, and checkpoints without moving execution logic into the Uno app.";
    private const string TrafficPolicyName = "Traffic policy";
    private const string TrafficPolicySummary =
        "Allowed grain transitions are explicit, testable, and surfaced through the embedded traffic-policy Mermaid catalog instead of hidden conventions.";
    private const string SessionPersistenceName = "Session persistence";
    private const string SessionPersistenceSummary =
        "Checkpoint, replay, and resume data survive host restarts in local session archives without changing the Orleans storage topology.";

    public RuntimeFoundationSnapshot GetSnapshot()
    {
        return new(
            RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.EmbeddedAgentRuntimeHostEpic),
            EpicSummary,
            ProviderToolchainNames.DeterministicClientDisplayName,
            DeterministicProbePrompt,
            CreateSlices(),
            CreateProviders());
    }

    private static IReadOnlyList<RuntimeSliceDescriptor> CreateSlices()
    {
        return
        [
            new(
                RuntimeFoundationIssues.DomainModel,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.DomainModel),
                DomainModelName,
                DomainModelSummary,
                RuntimeSliceState.ReadyForImplementation),
            new(
                RuntimeFoundationIssues.CommunicationContracts,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.CommunicationContracts),
                CommunicationName,
                CommunicationSummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.EmbeddedOrleansHost,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.EmbeddedOrleansHost),
                HostName,
                HostSummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.AgentFrameworkRuntime,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.AgentFrameworkRuntime),
                OrchestrationName,
                OrchestrationSummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.GrainTrafficPolicy,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.GrainTrafficPolicy),
                TrafficPolicyName,
                TrafficPolicySummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.SessionPersistence,
                RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.SessionPersistence),
                SessionPersistenceName,
                SessionPersistenceSummary,
                RuntimeSliceState.Sequenced),
        ];
    }

    private static IReadOnlyList<ProviderDescriptor> CreateProviders()
    {
        return
        [
            new ProviderDescriptor
            {
                Id = ProviderId.New(),
                DisplayName = ProviderToolchainNames.DeterministicClientDisplayName,
                CommandName = ProviderToolchainNames.DeterministicClientCommandName,
                Status = ProviderConnectionStatus.Available,
                StatusSummary = DeterministicClientStatusSummary,
                RequiresExternalToolchain = false,
            },
            .. ToolchainProviderSnapshotFactory.Create(TimeProvider.System.GetUtcNow())
                .Select(snapshot => snapshot.Provider),
        ];
    }
}
