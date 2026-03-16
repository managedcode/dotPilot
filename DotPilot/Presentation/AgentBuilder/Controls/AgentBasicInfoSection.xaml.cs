namespace DotPilot.Presentation.Controls;

public sealed partial class AgentBasicInfoSection : UserControl
{
    public static readonly DependencyProperty ProviderSelectionChangedCommandProperty =
        DependencyProperty.Register(
            nameof(ProviderSelectionChangedCommand),
            typeof(ICommand),
            typeof(AgentBasicInfoSection),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectModelCommandProperty =
        DependencyProperty.Register(
            nameof(SelectModelCommand),
            typeof(ICommand),
            typeof(AgentBasicInfoSection),
            new PropertyMetadata(null));

    public AgentBasicInfoSection()
    {
        InitializeComponent();
    }

    public ICommand? ProviderSelectionChangedCommand
    {
        get => (ICommand?)GetValue(ProviderSelectionChangedCommandProperty);
        set => SetValue(ProviderSelectionChangedCommandProperty, value);
    }

    public ICommand? SelectModelCommand
    {
        get => (ICommand?)GetValue(SelectModelCommandProperty);
        set => SetValue(SelectModelCommandProperty, value);
    }

    public bool IsBrowserHead => OperatingSystem.IsBrowser();

    private void OnProviderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var provider = e.AddedItems.OfType<AgentProviderOption>().FirstOrDefault();
        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Provider selection changed. Provider={provider?.Kind.ToString() ?? "<null>"}.");
        BoundCommandBridge.Execute(ProviderSelectionChangedCommand, provider);
    }

    private void OnProviderQuickSelectButtonClick(object sender, RoutedEventArgs e)
    {
        var provider = (sender as FrameworkElement)?.DataContext as AgentProviderOption;
        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Provider quick-select clicked. Provider={provider?.Kind.ToString() ?? "<null>"}.");
        BoundCommandBridge.Execute(ProviderSelectionChangedCommand, provider);
    }

    private void OnModelQuickSelectButtonClick(object sender, RoutedEventArgs e)
    {
        var model = (sender as FrameworkElement)?.DataContext as AgentModelOption;
        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Model quick-select clicked. Model={model?.DisplayName ?? "<null>"}.");
        BoundCommandBridge.Execute(SelectModelCommand, model);
    }
}
