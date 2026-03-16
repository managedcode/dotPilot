using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class SendSessionMessageCommand : Command<SendSessionMessageCommand.Payload>
{
    private readonly Payload payload;

    public SendSessionMessageCommand(
        SessionId sessionId,
        string message)
        : this(new Payload(sessionId, message))
    {
    }

    private SendSessionMessageCommand(Payload payload)
        : base(Guid.CreateVersion7(), nameof(SendSessionMessageCommand), payload)
    {
        this.payload = payload;
        Value = payload;
        base.SessionId = payload.SessionId.Value.ToString("N", CultureInfo.InvariantCulture);
    }

    public new SessionId SessionId => payload.SessionId;

    public string Message => payload.Message;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] SessionId SessionId,
        [property: Id(1)] string Message);
}
