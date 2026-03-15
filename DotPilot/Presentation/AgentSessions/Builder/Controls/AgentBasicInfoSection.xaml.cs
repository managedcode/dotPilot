namespace DotPilot.Presentation.Controls;

public sealed partial class AgentBasicInfoSection : UserControl
{
    public AgentBasicInfoSection()
    {
        InitializeComponent();
    }

    private void OnProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BoundCommandBridge.Execute(ProviderCombo.Tag as ICommand, ProviderCombo.SelectedItem);
    }
}
