using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.Communication.Commands;

namespace DotPilot.Core.ChatSessions.Commands;

public sealed class SendSessionMessageCommand : Command<SendSessionMessageCommand.Payload>
{
    private readonly Payload _payload;

    public SendSessionMessageCommand(
        SessionId sessionId,
        string message)
        : base(
            Guid.CreateVersion7(),
            nameof(SendSessionMessageCommand),
            new Payload(sessionId, message))
    {
        _payload = new Payload(sessionId, message);
        Value = _payload;
        base.SessionId = sessionId.Value.ToString("N", CultureInfo.InvariantCulture);
    }

    public new SessionId SessionId => _payload.SessionId;

    public string Message => _payload.Message;

    [GenerateSerializer]
    public sealed record Payload(
        [property: Id(0)] SessionId SessionId,
        [property: Id(1)] string Message);
}
