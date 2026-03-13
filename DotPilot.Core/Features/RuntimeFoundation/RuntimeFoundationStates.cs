namespace DotPilot.Core.Features.RuntimeFoundation;

public enum SessionPhase
{
    Plan,
    Execute,
    Review,
    Paused,
    Completed,
    Failed,
}

public enum ProviderConnectionStatus
{
    Available,
    Unavailable,
}

public enum ApprovalState
{
    NotRequired,
    Pending,
    Approved,
    Rejected,
}

public enum RuntimeSliceState
{
    Planned,
    Sequenced,
    ReadyForImplementation,
}

public enum AgentExecutionMode
{
    Plan,
    Execute,
    Review,
}
