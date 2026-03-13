namespace DotPilot.Tests;

public class RuntimeFoundationCatalogTests
{
    private const string ApprovalPrompt = "Please continue, but stop for approval before changing files.";
    private const string BlankPrompt = " ";
    private const string CodexCommandName = "codex";
    private const string ClaudeCommandName = "claude";
    private const string GitHubCommandName = "gh";

    [Test]
    public void CatalogGroupsEpicTwelveIntoFourSequencedSlices()
    {
        var catalog = CreateCatalog();

        var snapshot = catalog.GetSnapshot();

        snapshot.EpicLabel.Should().Be(RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.EmbeddedAgentRuntimeHostEpic));
        snapshot.Slices.Should().HaveCount(4);
        snapshot.Slices.Select(slice => slice.IssueNumber).Should().ContainInOrder(
            RuntimeFoundationIssues.DomainModel,
            RuntimeFoundationIssues.CommunicationContracts,
            RuntimeFoundationIssues.EmbeddedOrleansHost,
            RuntimeFoundationIssues.AgentFrameworkRuntime);
    }

    [Test]
    public void CatalogAlwaysIncludesTheDeterministicClientForProviderIndependentCoverage()
    {
        var catalog = CreateCatalog();

        var snapshot = catalog.GetSnapshot();

        snapshot.Providers.Should().ContainSingle(provider =>
            provider.DisplayName == snapshot.DeterministicClientName &&
            provider.RequiresExternalToolchain == false &&
            provider.Status == ProviderConnectionStatus.Available);
    }

    [Test]
    public async Task DeterministicClientReturnsPendingApprovalWhenPromptRequestsApproval()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest(ApprovalPrompt, AgentExecutionMode.Execute), CancellationToken.None);
        var outcome = result.Value!;

        result.IsSuccess.Should().BeTrue();
        outcome.NextPhase.Should().Be(SessionPhase.Paused);
        outcome.ApprovalState.Should().Be(ApprovalState.Pending);
        outcome.ProducedArtifacts.Should().ContainSingle(artifact =>
            artifact.Name == "runtime-foundation.snapshot.json" &&
            artifact.Kind == ArtifactKind.Snapshot);
    }

    [Test]
    public async Task DeterministicClientReturnsPlanArtifactsForPlanMode()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest("Plan the runtime foundation rollout.", AgentExecutionMode.Plan), CancellationToken.None);
        var outcome = result.Value!;

        result.IsSuccess.Should().BeTrue();
        outcome.NextPhase.Should().Be(SessionPhase.Plan);
        outcome.ApprovalState.Should().Be(ApprovalState.NotRequired);
        outcome.ProducedArtifacts.Should().ContainSingle(artifact =>
            artifact.Name == "runtime-foundation.plan.md" &&
            artifact.Kind == ArtifactKind.Plan);
    }

    [Test]
    public async Task DeterministicClientReturnsExecuteResultsWhenApprovalIsNotRequested()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest("Run the provider-independent runtime flow.", AgentExecutionMode.Execute), CancellationToken.None);
        var outcome = result.Value!;

        result.IsSuccess.Should().BeTrue();
        outcome.NextPhase.Should().Be(SessionPhase.Execute);
        outcome.ApprovalState.Should().Be(ApprovalState.NotRequired);
        outcome.ProducedArtifacts.Should().ContainSingle(artifact =>
            artifact.Name == "runtime-foundation.snapshot.json" &&
            artifact.Kind == ArtifactKind.Snapshot);
    }

    [Test]
    public async Task DeterministicClientReturnsValidationProblemForBlankPrompts()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest(BlankPrompt, AgentExecutionMode.Plan), CancellationToken.None);
        var problem = result.Problem!;

        result.IsFailed.Should().BeTrue();
        result.HasProblem.Should().BeTrue();
        problem.HasErrorCode(RuntimeCommunicationProblemCode.PromptRequired).Should().BeTrue();
        problem.InvalidField("Prompt").Should().BeTrue();
    }

    [Test]
    public async Task DeterministicClientHonorsCancellationBeforeProcessing()
    {
        var client = new DeterministicAgentRuntimeClient();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await client.ExecuteAsync(CreateRequest("Plan the runtime foundation rollout.", AgentExecutionMode.Plan), cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task DeterministicClientReturnsApprovedReviewResults()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest("Review the runtime foundation output.", AgentExecutionMode.Review), CancellationToken.None);
        var outcome = result.Value!;

        result.IsSuccess.Should().BeTrue();
        outcome.NextPhase.Should().Be(SessionPhase.Review);
        outcome.ApprovalState.Should().Be(ApprovalState.Approved);
        outcome.ProducedArtifacts.Should().ContainSingle(artifact =>
            artifact.Name == "runtime-foundation.review.md" &&
            artifact.Kind == ArtifactKind.Report);
    }

    [Test]
    public async Task DeterministicClientReturnsProviderUnavailableProblemWhenProviderIsNotReady()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(
            CreateRequest(
                "Run the provider-independent runtime flow.",
                AgentExecutionMode.Execute,
                ProviderConnectionStatus.Unavailable),
            CancellationToken.None);
        var problem = result.Problem!;

        result.IsFailed.Should().BeTrue();
        result.HasProblem.Should().BeTrue();
        problem.HasErrorCode(RuntimeCommunicationProblemCode.ProviderUnavailable).Should().BeTrue();
        problem.StatusCode.Should().Be((int)System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [TestCase(CodexCommandName)]
    [TestCase(ClaudeCommandName)]
    [TestCase(GitHubCommandName)]
    public void ExternalToolchainVerificationRunsOnlyWhenTheCommandIsAvailable(string commandName)
    {
        var catalog = CreateCatalog();
        var provider = catalog.GetSnapshot().Providers.Single(item => item.CommandName == commandName);

        Assume.That(
            provider.Status,
            Is.EqualTo(ProviderConnectionStatus.Available),
            $"The '{commandName}' toolchain is not available in this environment.");

        provider.RequiresExternalToolchain.Should().BeTrue();
        provider.StatusSummary.Should().Contain("available");
    }

    [Test]
    public void TypedIdentifiersProduceStableNonEmptyRepresentations()
    {
        IReadOnlyList<string> values =
        [
            WorkspaceId.New().ToString(),
            AgentProfileId.New().ToString(),
            SessionId.New().ToString(),
            FleetId.New().ToString(),
            ProviderId.New().ToString(),
            ModelRuntimeId.New().ToString(),
        ];

        values.Should().OnlyContain(value => !string.IsNullOrWhiteSpace(value));
        values.Should().OnlyHaveUniqueItems();
    }

    [Test]
    [NonParallelizable]
    public void ExternalProvidersBecomeUnavailableWhenPathIsCleared()
    {
        using var scope = new EnvironmentVariableScope("PATH", string.Empty);
        var catalog = CreateCatalog();

        var externalProviders = catalog.GetSnapshot().Providers.Where(provider => provider.RequiresExternalToolchain);

        externalProviders.Should().OnlyContain(provider => provider.Status == ProviderConnectionStatus.Unavailable);
    }

    private static RuntimeFoundationCatalog CreateCatalog()
    {
        return new RuntimeFoundationCatalog(new DeterministicAgentRuntimeClient());
    }

    private static AgentTurnRequest CreateRequest(
        string prompt,
        AgentExecutionMode mode,
        ProviderConnectionStatus providerStatus = ProviderConnectionStatus.Available)
    {
        return new AgentTurnRequest(SessionId.New(), AgentProfileId.New(), prompt, mode, providerStatus);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _variableName;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string variableName, string? value)
        {
            _variableName = variableName;
            _originalValue = Environment.GetEnvironmentVariable(variableName);
            Environment.SetEnvironmentVariable(variableName, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_variableName, _originalValue);
        }
    }
}
