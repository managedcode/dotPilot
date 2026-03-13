using System.Globalization;
using System.Net;
using DotPilot.Core.Features.ControlPlaneDomain;
using ManagedCode.Communication;

namespace DotPilot.Core.Features.RuntimeCommunication;

public static class RuntimeCommunicationProblems
{
    private const string PromptField = "Prompt";
    private const string PromptRequiredDetail = "Prompt is required before the runtime can execute a turn.";
    private const string ProviderUnavailableFormat = "{0} is unavailable in the current environment.";
    private const string ProviderAuthenticationRequiredFormat = "{0} requires authentication before the runtime can execute a turn.";
    private const string ProviderMisconfiguredFormat = "{0} is misconfigured and cannot execute a runtime turn.";
    private const string ProviderOutdatedFormat = "{0} is outdated and must be updated before the runtime can execute a turn.";
    private const string RuntimeHostUnavailableDetail = "The embedded runtime host is unavailable for the requested operation.";
    private const string OrchestrationUnavailableDetail = "The orchestration runtime is unavailable for the requested operation.";
    private const string PolicyRejectedFormat = "The requested action was rejected by policy: {0}.";

    public static Problem InvalidPrompt()
    {
        var problem = Problem.Create(
            RuntimeCommunicationProblemCode.PromptRequired,
            PromptRequiredDetail,
            (int)HttpStatusCode.BadRequest);

        problem.AddValidationError(PromptField, PromptRequiredDetail);
        return problem;
    }

    public static Problem ProviderUnavailable(ProviderConnectionStatus status, string providerDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerDisplayName);

        return status switch
        {
            ProviderConnectionStatus.Available => throw new ArgumentOutOfRangeException(nameof(status), status, "Available status does not map to a problem."),
            ProviderConnectionStatus.Unavailable => CreateProblem(
                RuntimeCommunicationProblemCode.ProviderUnavailable,
                ProviderUnavailableFormat,
                providerDisplayName,
                HttpStatusCode.ServiceUnavailable),
            ProviderConnectionStatus.RequiresAuthentication => CreateProblem(
                RuntimeCommunicationProblemCode.ProviderAuthenticationRequired,
                ProviderAuthenticationRequiredFormat,
                providerDisplayName,
                HttpStatusCode.Unauthorized),
            ProviderConnectionStatus.Misconfigured => CreateProblem(
                RuntimeCommunicationProblemCode.ProviderMisconfigured,
                ProviderMisconfiguredFormat,
                providerDisplayName,
                HttpStatusCode.FailedDependency),
            ProviderConnectionStatus.Outdated => CreateProblem(
                RuntimeCommunicationProblemCode.ProviderOutdated,
                ProviderOutdatedFormat,
                providerDisplayName,
                HttpStatusCode.PreconditionFailed),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown provider status."),
        };
    }

    public static Problem RuntimeHostUnavailable()
    {
        return Problem.Create(
            RuntimeCommunicationProblemCode.RuntimeHostUnavailable,
            RuntimeHostUnavailableDetail,
            (int)HttpStatusCode.ServiceUnavailable);
    }

    public static Problem OrchestrationUnavailable()
    {
        return Problem.Create(
            RuntimeCommunicationProblemCode.OrchestrationUnavailable,
            OrchestrationUnavailableDetail,
            (int)HttpStatusCode.ServiceUnavailable);
    }

    public static Problem PolicyRejected(string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        return CreateProblem(
            RuntimeCommunicationProblemCode.PolicyRejected,
            PolicyRejectedFormat,
            policyName,
            HttpStatusCode.Forbidden);
    }

    private static Problem CreateProblem(
        RuntimeCommunicationProblemCode code,
        string detailFormat,
        string value,
        HttpStatusCode statusCode)
    {
        return Problem.Create(
            code,
            string.Format(CultureInfo.InvariantCulture, detailFormat, value),
            (int)statusCode);
    }
}
