using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class CloseSessionCommand : Command<CloseSessionCommand.Payload>
{
    private readonly Payload payload;

    public CloseSessionCommand(SessionId sessionId)
        : this(new Payload(sessionId))
    {
    }

    private CloseSessionCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(CloseSessionCommand), payload)
    {
        this.payload = payload;
        Value = payload;
    }

    public new SessionId SessionId => payload.SessionId;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] SessionId SessionId);
}
