namespace DotPilot.Presentation;

public enum ShellRoute
{
    Chat,
    Agents,
    Settings,
}

public sealed class ShellNavigationNotifier
{
    public event EventHandler<ShellNavigationRequestedEventArgs>? Requested;

    public void Request(ShellRoute route)
    {
        Requested?.Invoke(this, new ShellNavigationRequestedEventArgs(route));
    }
}

public sealed class ShellNavigationRequestedEventArgs(ShellRoute route) : EventArgs
{
    public ShellRoute Route { get; } = route;
}
