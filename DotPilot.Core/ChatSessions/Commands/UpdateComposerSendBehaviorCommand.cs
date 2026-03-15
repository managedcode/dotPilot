using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class UpdateComposerSendBehaviorCommand : Command<UpdateComposerSendBehaviorCommand.Payload>
{
    private readonly Payload _payload;

    public UpdateComposerSendBehaviorCommand(ComposerSendBehavior behavior)
        : base(
            Guid.CreateVersion7(),
            nameof(UpdateComposerSendBehaviorCommand),
            new Payload(behavior))
    {
        _payload = new Payload(behavior);
        Value = _payload;
    }

    public ComposerSendBehavior Behavior => _payload.Behavior;

    [GenerateSerializer]
    public sealed record Payload([property: Id(0)] ComposerSendBehavior Behavior);
}
