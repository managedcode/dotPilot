namespace DotPilot.Tests.Features.RuntimeCommunication;

public class RuntimeCommunicationProblemsTests
{
    [TestCase(ProviderConnectionStatus.Unavailable, RuntimeCommunicationProblemCode.ProviderUnavailable, System.Net.HttpStatusCode.ServiceUnavailable)]
    [TestCase(ProviderConnectionStatus.RequiresAuthentication, RuntimeCommunicationProblemCode.ProviderAuthenticationRequired, System.Net.HttpStatusCode.Unauthorized)]
    [TestCase(ProviderConnectionStatus.Misconfigured, RuntimeCommunicationProblemCode.ProviderMisconfigured, System.Net.HttpStatusCode.FailedDependency)]
    [TestCase(ProviderConnectionStatus.Outdated, RuntimeCommunicationProblemCode.ProviderOutdated, System.Net.HttpStatusCode.PreconditionFailed)]
    public void ProviderUnavailableMapsStatusesToExplicitProblemCodes(
        ProviderConnectionStatus status,
        RuntimeCommunicationProblemCode expectedCode,
        System.Net.HttpStatusCode expectedStatusCode)
    {
        var problem = RuntimeCommunicationProblems.ProviderUnavailable(status, "Codex");

        problem.HasErrorCode(expectedCode).Should().BeTrue();
        problem.StatusCode.Should().Be((int)expectedStatusCode);
        problem.Detail.Should().Contain("Codex");
    }

    [Test]
    public void ProviderUnavailableRejectsAvailableStatus()
    {
        var action = () => RuntimeCommunicationProblems.ProviderUnavailable(ProviderConnectionStatus.Available, "Codex");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ProviderUnavailableRejectsBlankProviderNames()
    {
        var action = () => RuntimeCommunicationProblems.ProviderUnavailable(ProviderConnectionStatus.Unavailable, " ");

        action.Should().Throw<ArgumentException>();
    }

    [Test]
    public void InvalidPromptCreatesValidationProblem()
    {
        var problem = RuntimeCommunicationProblems.InvalidPrompt();

        problem.HasErrorCode(RuntimeCommunicationProblemCode.PromptRequired).Should().BeTrue();
        problem.StatusCode.Should().Be((int)System.Net.HttpStatusCode.BadRequest);
        problem.InvalidField("Prompt").Should().BeTrue();
    }

    [Test]
    public void RuntimeHostUnavailableCreatesServiceUnavailableProblem()
    {
        var problem = RuntimeCommunicationProblems.RuntimeHostUnavailable();

        problem.HasErrorCode(RuntimeCommunicationProblemCode.RuntimeHostUnavailable).Should().BeTrue();
        problem.StatusCode.Should().Be((int)System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public void OrchestrationUnavailableCreatesServiceUnavailableProblem()
    {
        var problem = RuntimeCommunicationProblems.OrchestrationUnavailable();

        problem.HasErrorCode(RuntimeCommunicationProblemCode.OrchestrationUnavailable).Should().BeTrue();
        problem.StatusCode.Should().Be((int)System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public void PolicyRejectedCreatesForbiddenProblem()
    {
        var problem = RuntimeCommunicationProblems.PolicyRejected("file-write policy");

        problem.HasErrorCode(RuntimeCommunicationProblemCode.PolicyRejected).Should().BeTrue();
        problem.StatusCode.Should().Be((int)System.Net.HttpStatusCode.Forbidden);
        problem.Detail.Should().Contain("file-write policy");
    }

    [Test]
    public void PolicyRejectedRejectsBlankPolicyNames()
    {
        var action = () => RuntimeCommunicationProblems.PolicyRejected(" ");

        action.Should().Throw<ArgumentException>();
    }
}
