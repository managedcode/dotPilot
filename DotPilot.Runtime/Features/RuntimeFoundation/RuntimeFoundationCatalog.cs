using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;
using DotPilot.Runtime.Features.ToolchainCenter;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class RuntimeFoundationCatalog : IRuntimeFoundationCatalog
{
    private const string EpicSummary =
        "Issue #12 is staged into isolated contracts, communication, host, and orchestration slices so the Uno workbench can stay presentation-only.";
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
        "Agent Framework integration is prepared as a separate slice that can plug into the embedded host without reshaping the UI layer.";

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
