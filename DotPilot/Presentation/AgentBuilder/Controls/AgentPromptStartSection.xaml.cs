namespace DotPilot.Presentation.Controls;

public sealed partial class AgentPromptStartSection : UserControl
{
    public AgentPromptStartSection()
    {
        InitializeComponent();
    }

    private void OnGenerateAgentButtonClick(object sender, RoutedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info("[DotPilot.AgentBuilder] Generate agent click received.");
        BoundCommandBridge.Execute((sender as FrameworkElement)?.Tag as ICommand, PromptInput.Text);
    }

    private void OnBuildManuallyButtonClick(object sender, RoutedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info("[DotPilot.AgentBuilder] Build manually click received.");
        BoundCommandBridge.Execute((sender as FrameworkElement)?.Tag as ICommand);
    }
}
