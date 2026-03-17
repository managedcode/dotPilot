using DotPilot.Core;

namespace DotPilot.Presentation;

public sealed class SessionSelectionNotifier
{
    public event EventHandler<SessionSelectionRequestedEventArgs>? Requested;

    public void Request(SessionId sessionId)
    {
        Requested?.Invoke(this, new SessionSelectionRequestedEventArgs(sessionId));
    }
}

public sealed class SessionSelectionRequestedEventArgs(SessionId sessionId) : EventArgs
{
    public SessionId SessionId { get; } = sessionId;
}
