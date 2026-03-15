namespace DotPilot.Presentation.Controls;

public sealed partial class AgentPromptSection : UserControl
{
    public AgentPromptSection()
    {
        InitializeComponent();
    }

    private void OnSaveAgentButtonClick(object sender, RoutedEventArgs e)
    {
        BoundCommandBridge.Execute(SaveAgentActionButton.Tag as ICommand);
    }
}
