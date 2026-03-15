namespace DotPilot.Presentation.Controls;

public sealed partial class AgentBasicInfoSection : UserControl
{
    public AgentBasicInfoSection()
    {
        InitializeComponent();
    }

    public bool IsBrowserHead => OperatingSystem.IsBrowser();

    public bool IsDesktopHead => !IsBrowserHead;

    private void OnProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox)
        {
            return;
        }

        AgentProviderKind? selectedProviderKind = e.AddedItems.OfType<AgentProviderOption>().FirstOrDefault()?.Kind;
        selectedProviderKind ??= comboBox.SelectedValue as AgentProviderKind?;
        if (selectedProviderKind is null &&
            comboBox.SelectedItem is AgentProviderOption selectedProvider)
        {
            selectedProviderKind = selectedProvider.Kind;
        }

        _ = comboBox.DispatcherQueue.TryEnqueue(() =>
        {
            BrowserConsoleDiagnostics.Info(
                $"[DotPilot.AgentBuilder] Provider selection changed. Provider={selectedProviderKind?.ToString() ?? "<null>"}.");
            BoundCommandBridge.Execute(comboBox.Tag as ICommand, selectedProviderKind);
        });
    }

    private void OnProviderQuickSelectButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Provider quick-select clicked. Provider={button.CommandParameter?.ToString() ?? "<null>"}.");
        BoundCommandBridge.Execute(button.Tag as ICommand, button.CommandParameter);
    }

    private void OnModelQuickSelectButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Model quick-select clicked. Model={button.CommandParameter?.ToString() ?? "<null>"}.");
        BoundCommandBridge.Execute(button.Tag as ICommand, button.CommandParameter);
    }
}
