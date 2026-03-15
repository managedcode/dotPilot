namespace DotPilot.Presentation.Controls;

public sealed partial class AgentCatalogSection : UserControl
{
    public static readonly DependencyProperty OpenCreateAgentCommandProperty =
        DependencyProperty.Register(
            nameof(OpenCreateAgentCommand),
            typeof(ICommand),
            typeof(AgentCatalogSection),
            new PropertyMetadata(null));

    public AgentCatalogSection()
    {
        InitializeComponent();
    }

    public ICommand? OpenCreateAgentCommand
    {
        get => (ICommand?)GetValue(OpenCreateAgentCommandProperty);
        set => SetValue(OpenCreateAgentCommandProperty, value);
    }

    private void OnOpenCreateAgentButtonClick(object sender, RoutedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Open create agent click received. CommandNull={OpenCreateAgentCommand is null}.");
        BoundCommandBridge.Execute(OpenCreateAgentCommand);
    }
}
