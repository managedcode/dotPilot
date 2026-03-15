using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CreateSessionCommand : Command<CreateSessionCommand.Payload>
{
    private readonly Payload _payload;

    public CreateSessionCommand(
        string title,
        AgentProfileId agentProfileId)
        : base(
            Guid.CreateVersion7(),
            nameof(CreateSessionCommand),
            new Payload(title, agentProfileId))
    {
        _payload = new Payload(title, agentProfileId);
        Value = _payload;
    }

    public string Title => _payload.Title;

    public AgentProfileId AgentProfileId => _payload.AgentProfileId;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] string Title,
        [property: Id(1)] AgentProfileId AgentProfileId);
}
