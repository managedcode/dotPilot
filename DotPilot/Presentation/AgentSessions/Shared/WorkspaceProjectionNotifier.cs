namespace DotPilot.Presentation;

public sealed class WorkspaceProjectionNotifier
{
    public event EventHandler? Changed;

    public void Publish()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
