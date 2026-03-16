using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CreateSessionCommand : Command<CreateSessionCommand.Payload>
{
    private readonly Payload payload;

    public CreateSessionCommand(
        string title,
        AgentProfileId agentProfileId)
        : this(new Payload(title, agentProfileId))
    {
    }

    private CreateSessionCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(CreateSessionCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public string Title => payload.Title;

    public AgentProfileId AgentProfileId => payload.AgentProfileId;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] string Title,
        [property: Id(1)] AgentProfileId AgentProfileId);
}
