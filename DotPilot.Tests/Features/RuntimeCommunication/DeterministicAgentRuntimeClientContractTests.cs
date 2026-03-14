namespace DotPilot.Tests.Features.RuntimeCommunication;

public sealed class DeterministicAgentRuntimeClientContractTests
{
    private const string ApprovalPrompt = "Execute the local-first flow and request approval before changing files.";

    [Test]
    public async Task ExecuteAsyncReturnsSucceededResultWithoutProblemForPlanMode()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest("Plan the contract foundation rollout.", AgentExecutionMode.Plan), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.IsFailed.Should().BeFalse();
        result.HasProblem.Should().BeFalse();
        result.Value.Should().NotBeNull();
        result.Value!.NextPhase.Should().Be(SessionPhase.Plan);
    }

    [Test]
    public async Task ExecuteAsyncTreatsApprovalPauseAsASuccessfulStateTransition()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(CreateRequest(ApprovalPrompt, AgentExecutionMode.Execute), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.HasProblem.Should().BeFalse();
        result.Value.Should().NotBeNull();
        result.Value!.NextPhase.Should().Be(SessionPhase.Paused);
        result.Value.ApprovalState.Should().Be(ApprovalState.Pending);
    }

    [TestCase(ProviderConnectionStatus.Unavailable, RuntimeCommunicationProblemCode.ProviderUnavailable, 503)]
    [TestCase(ProviderConnectionStatus.RequiresAuthentication, RuntimeCommunicationProblemCode.ProviderAuthenticationRequired, 401)]
    [TestCase(ProviderConnectionStatus.Misconfigured, RuntimeCommunicationProblemCode.ProviderMisconfigured, 424)]
    [TestCase(ProviderConnectionStatus.Outdated, RuntimeCommunicationProblemCode.ProviderOutdated, 412)]
    public async Task ExecuteAsyncMapsProviderStatesToTypedProblems(
        ProviderConnectionStatus providerStatus,
        RuntimeCommunicationProblemCode expectedCode,
        int expectedStatusCode)
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(
            CreateRequest("Run the provider-independent runtime flow.", AgentExecutionMode.Execute, providerStatus),
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasProblem.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Problem.Should().NotBeNull();
        result.Problem!.HasErrorCode(expectedCode).Should().BeTrue();
        result.Problem.StatusCode.Should().Be(expectedStatusCode);
    }

    [Test]
    public async Task ExecuteAsyncReturnsOrchestrationProblemForUnsupportedExecutionModes()
    {
        var client = new DeterministicAgentRuntimeClient();

        var result = await client.ExecuteAsync(
            CreateRequest("Use an invalid execution mode.", (AgentExecutionMode)999),
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.HasProblem.Should().BeTrue();
        result.Value.Should().BeNull();
        result.Problem.Should().NotBeNull();
        result.Problem!.HasErrorCode(RuntimeCommunicationProblemCode.OrchestrationUnavailable).Should().BeTrue();
        result.Problem.StatusCode.Should().Be(503);
    }

    private static AgentTurnRequest CreateRequest(
        string prompt,
        AgentExecutionMode mode,
        ProviderConnectionStatus providerStatus = ProviderConnectionStatus.Available)
    {
        return new AgentTurnRequest(SessionId.New(), AgentProfileId.New(), prompt, mode, providerStatus);
    }
}
