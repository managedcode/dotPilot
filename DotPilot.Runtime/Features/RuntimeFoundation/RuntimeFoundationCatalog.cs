using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;
namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class RuntimeFoundationCatalog : IRuntimeFoundationCatalog
{
    private const string EpicSummary =
        "Runtime contracts, host sequencing, and orchestration seams stay isolated so the Uno app can remain presentation-only.";
    private const string EpicLabelValue = "LOCAL RUNTIME READINESS";
    private const string DeterministicProbePrompt =
        "Summarize the runtime foundation readiness for a local-first session that may require approval.";
    private const string DeterministicClientStatusSummary = "Always available for in-repo and CI validation.";
    private const string DomainModelLabel = "DOMAIN";
    private const string DomainModelName = "Domain contracts";
    private const string DomainModelSummary =
        "Typed identifiers and durable agent, session, fleet, provider, and runtime contracts live outside the Uno app.";
    private const string CommunicationLabel = "CONTRACTS";
    private const string CommunicationName = "Communication contracts";
    private const string CommunicationSummary =
        "Public result and problem boundaries are isolated so later provider and orchestration slices share one contract language.";
    private const string HostLabel = "HOST";
    private const string HostName = "Embedded host";
    private const string HostSummary =
        "The Orleans host integration point is sequenced behind dedicated runtime contracts instead of being baked into page code.";
    private const string OrchestrationLabel = "ORCHESTRATION";
    private const string OrchestrationName = "Orchestration runtime";
    private const string OrchestrationSummary =
        "Agent Framework integration is prepared as a separate slice that can plug into the embedded host without reshaping the UI layer.";
    private readonly IReadOnlyList<ProviderDescriptor> _providers;

    public RuntimeFoundationCatalog() => _providers = Array.AsReadOnly(CreateProviders());

    public RuntimeFoundationSnapshot GetSnapshot()
    {
        return new(
            EpicLabelValue,
            EpicSummary,
            ProviderToolchainNames.DeterministicClientDisplayName,
            DeterministicProbePrompt,
            CreateSlices(),
            _providers);
    }

    private static IReadOnlyList<RuntimeSliceDescriptor> CreateSlices()
    {
        return
        [
            new(
                RuntimeFoundationIssues.DomainModel,
                DomainModelLabel,
                DomainModelName,
                DomainModelSummary,
                RuntimeSliceState.ReadyForImplementation),
            new(
                RuntimeFoundationIssues.CommunicationContracts,
                CommunicationLabel,
                CommunicationName,
                CommunicationSummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.EmbeddedOrleansHost,
                HostLabel,
                HostName,
                HostSummary,
                RuntimeSliceState.Sequenced),
            new(
                RuntimeFoundationIssues.AgentFrameworkRuntime,
                OrchestrationLabel,
                OrchestrationName,
                OrchestrationSummary,
                RuntimeSliceState.Sequenced),
        ];
    }

    private static ProviderDescriptor[] CreateProviders()
    {
        return
        [
            new ProviderDescriptor
            {
                Id = RuntimeFoundationDeterministicIdentity.CreateProviderId(ProviderToolchainNames.DeterministicClientCommandName),
                DisplayName = ProviderToolchainNames.DeterministicClientDisplayName,
                CommandName = ProviderToolchainNames.DeterministicClientCommandName,
                Status = ProviderConnectionStatus.Available,
                StatusSummary = DeterministicClientStatusSummary,
                RequiresExternalToolchain = false,
            },
        ];
    }
}
