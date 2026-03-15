namespace DotPilot.Presentation.Controls;

public sealed partial class AgentPromptStartSection : UserControl
{
    public AgentPromptStartSection()
    {
        InitializeComponent();
    }

    private void OnGenerateAgentButtonClick(object sender, RoutedEventArgs e)
    {
        BoundCommandBridge.Execute(GenerateAgentButton.Tag as ICommand, PromptInput.Text);
    }
}
