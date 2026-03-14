namespace DotPilot.Core.Features.RuntimeCommunication;

public enum RuntimeCommunicationProblemCode
{
    PromptRequired,
    ProviderUnavailable,
    ProviderAuthenticationRequired,
    ProviderMisconfigured,
    ProviderOutdated,
    RuntimeHostUnavailable,
    OrchestrationUnavailable,
    PolicyRejected,
    SessionArchiveMissing,
    ResumeCheckpointMissing,
    SessionArchiveCorrupted,
}
