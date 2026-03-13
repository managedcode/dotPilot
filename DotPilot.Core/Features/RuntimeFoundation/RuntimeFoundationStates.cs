namespace DotPilot.Core.Features.RuntimeFoundation;

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
