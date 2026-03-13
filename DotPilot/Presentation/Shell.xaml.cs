namespace DotPilot.Presentation;

public sealed partial class Shell : UserControl, IContentControlProvider
{
    public Shell()
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] Shell constructor started.");
            InitializeComponent();
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] Shell constructor completed.");
        }
        catch (Exception exception)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Startup] Shell constructor failed: {exception}");
            throw;
        }
    }
    public ContentControl ContentControl => Splash;
}
