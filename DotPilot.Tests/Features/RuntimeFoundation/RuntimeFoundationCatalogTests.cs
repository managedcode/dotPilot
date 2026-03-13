namespace DotPilot.Tests.Features.RuntimeFoundation;

public class RuntimeFoundationCatalogTests
{
    private const string ApprovalPrompt = "Please continue, but stop for approval before changing files.";
    private const string BlankPrompt = " ";
    private const string DeterministicClientStatusSummary = "Always available for in-repo and CI validation.";
    private const string RuntimeEpicLabel = "LOCAL RUNTIME READINESS";
    private static readonly DateTimeOffset DeterministicArtifactCreatedAt = new(2026, 3, 13, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void CatalogGroupsEpicTwelveIntoFourSequencedSlices()
    {
        var catalog = CreateCatalog();

        var snapshot = catalog.GetSnapshot();

        snapshot.EpicLabel.Should().Be(RuntimeEpicLabel);
        snapshot.Slices.Should().HaveCount(4);
        snapshot.Slices.Select(slice => slice.IssueLabel).Should().ContainInOrder("DOMAIN", "CONTRACTS", "HOST", "ORCHESTRATION");
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
            provider.StatusSummary == DeterministicClientStatusSummary &&
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
    public async Task DeterministicClientProducesStableArtifactsForIdenticalRequests()
    {
        var client = new DeterministicAgentRuntimeClient();
        var request = CreateRequest("Run the provider-independent runtime flow.", AgentExecutionMode.Execute);

        var firstResult = await client.ExecuteAsync(request, CancellationToken.None);
        var secondResult = await client.ExecuteAsync(request, CancellationToken.None);
        var firstArtifact = firstResult.Value!.ProducedArtifacts.Should().ContainSingle().Subject;
        var secondArtifact = secondResult.Value!.ProducedArtifacts.Should().ContainSingle().Subject;

        firstResult.IsSuccess.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue();
        firstArtifact.Id.Should().Be(secondArtifact.Id);
        firstArtifact.CreatedAt.Should().Be(DeterministicArtifactCreatedAt);
        secondArtifact.CreatedAt.Should().Be(DeterministicArtifactCreatedAt);
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
        var snapshot = CreateCatalog().GetSnapshot();

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
        problem.Detail.Should().Contain(snapshot.DeterministicClientName);
    }

    [Test]
    public void DeterministicClientRejectsUnexpectedExecutionModes()
    {
        var client = new DeterministicAgentRuntimeClient();
        var invalidRequest = CreateRequest("Plan the runtime foundation rollout.", (AgentExecutionMode)int.MaxValue);

        var action = () => client.ExecuteAsync(invalidRequest, CancellationToken.None);

        action.Should().Throw<System.Diagnostics.UnreachableException>();
    }

    [Test]
    public void CatalogPreservesProviderIdentityAcrossSnapshotRefreshes()
    {
        var catalog = CreateCatalog();

        var firstSnapshot = catalog.GetSnapshot();
        var secondSnapshot = catalog.GetSnapshot();

        firstSnapshot.Providers.Should().HaveSameCount(secondSnapshot.Providers);
        foreach (var firstProvider in firstSnapshot.Providers)
        {
            var secondProvider = secondSnapshot.Providers.Single(provider => provider.CommandName == firstProvider.CommandName);
            firstProvider.Id.Should().Be(secondProvider.Id);
        }
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
    public void CatalogCachesProviderListAcrossSnapshotReads()
    {
        var catalog = CreateCatalog();

        var firstSnapshot = catalog.GetSnapshot();
        var secondSnapshot = catalog.GetSnapshot();

        ReferenceEquals(firstSnapshot.Providers, secondSnapshot.Providers).Should().BeTrue();
    }

    private static RuntimeFoundationCatalog CreateCatalog()
    {
        return new RuntimeFoundationCatalog();
    }

    private static AgentTurnRequest CreateRequest(
        string prompt,
        AgentExecutionMode mode,
        ProviderConnectionStatus providerStatus = ProviderConnectionStatus.Available)
    {
        return new AgentTurnRequest(SessionId.New(), AgentProfileId.New(), prompt, mode, providerStatus);
    }
}
