namespace DotPilot.Core.ChatSessions;

public interface ISessionActivityMonitor
{
    SessionActivitySnapshot Current { get; }

    event EventHandler? StateChanged;

    IDisposable BeginActivity(SessionActivityDescriptor descriptor);
}
