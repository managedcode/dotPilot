namespace DotPilot.Presentation.Controls;

public sealed partial class SettingsShell : UserControl
{
    public static readonly DependencyProperty SelectProviderCommandProperty =
        DependencyProperty.Register(
            nameof(SelectProviderCommand),
            typeof(ICommand),
            typeof(SettingsShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ToggleSelectedProviderCommandProperty =
        DependencyProperty.Register(
            nameof(ToggleSelectedProviderCommand),
            typeof(ICommand),
            typeof(SettingsShell),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExecuteProviderActionCommandProperty =
        DependencyProperty.Register(
            nameof(ExecuteProviderActionCommand),
            typeof(ICommand),
            typeof(SettingsShell),
            new PropertyMetadata(null));

    public SettingsShell()
    {
        InitializeComponent();
    }

    public ICommand? SelectProviderCommand
    {
        get => (ICommand?)GetValue(SelectProviderCommandProperty);
        set => SetValue(SelectProviderCommandProperty, value);
    }

    public ICommand? ToggleSelectedProviderCommand
    {
        get => (ICommand?)GetValue(ToggleSelectedProviderCommandProperty);
        set => SetValue(ToggleSelectedProviderCommandProperty, value);
    }

    public ICommand? ExecuteProviderActionCommand
    {
        get => (ICommand?)GetValue(ExecuteProviderActionCommandProperty);
        set => SetValue(ExecuteProviderActionCommandProperty, value);
    }

    private void OnProviderButtonClick(object sender, RoutedEventArgs e)
    {
        var provider = (sender as FrameworkElement)?.Tag as ProviderStatusItem;
        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.Settings] Provider entry clicked. Provider={provider?.Kind.ToString() ?? "<null>"}.");
        BoundCommandBridge.Execute(SelectProviderCommand, provider);
    }

    private void OnToggleProviderButtonClick(object sender, RoutedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info("[DotPilot.Settings] Toggle provider click received.");
        BoundCommandBridge.Execute(ToggleSelectedProviderCommand);
    }

    private void OnProviderActionButtonClick(object sender, RoutedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info("[DotPilot.Settings] Provider action click received.");
        BoundCommandBridge.Execute(ExecuteProviderActionCommand, (sender as FrameworkElement)?.Tag);
    }
}
