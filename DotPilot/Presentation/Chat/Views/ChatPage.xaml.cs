namespace DotPilot.Presentation;

public sealed partial class ChatPage : Page
{
    public ChatPage()
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] ChatPage constructor started.");
            InitializeComponent();
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] ChatPage constructor completed.");
        }
        catch (Exception exception)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Startup] ChatPage constructor failed: {exception}");
            throw;
        }
    }
}
