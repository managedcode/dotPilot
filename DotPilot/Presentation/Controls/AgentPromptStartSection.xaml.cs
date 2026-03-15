namespace DotPilot.Presentation.Controls;

public sealed partial class AgentPromptStartSection : UserControl
{
    public AgentPromptStartSection()
    {
        InitializeComponent();
    }

    private void OnGenerateAgentButtonClick(object sender, RoutedEventArgs e)
    {
        ExecuteBoundCommand(GenerateAgentButton, PromptInput.Text);
    }

    private static void ExecuteBoundCommand(Button button, object? parameterOverride = null)
    {
        ArgumentNullException.ThrowIfNull(button);

        var command = button.Command;
        if (command is null)
        {
            return;
        }

        var parameter = parameterOverride ?? button.CommandParameter;
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }
}
