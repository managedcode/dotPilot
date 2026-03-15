namespace DotPilot.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] MainPage constructor started.");
            InitializeComponent();
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] MainPage constructor completed.");
        }
        catch (Exception exception)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Startup] MainPage constructor failed: {exception}");
            throw;
        }
    }
}
