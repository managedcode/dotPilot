namespace DotPilot.Presentation;

public sealed partial class Shell : Page, IContentControlProvider
{
    private const string SidebarButtonStyleKey = "SidebarButtonStyle";
    private const string SidebarButtonSelectedStyleKey = "SidebarButtonSelectedStyle";
    private const string MainRoute = "Main";
    private const string SecondRoute = "Second";
    private const string SettingsRoute = "Settings";
    private const string UnknownContentTypeName = "<null>";
    private string _currentRoute = MainRoute;

    public Shell()
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] Shell constructor started.");
            InitializeComponent();
            RegisterContentHostObserver();
            UpdateNavigationSelection(MainRoute);
            UpdateNavigationSelectionFromContent();
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] Shell constructor completed.");
        }
        catch (Exception exception)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Startup] Shell constructor failed: {exception}");
            throw;
        }
    }

    public ContentControl ContentControl => ContentHost;

    private void OnChatNavButtonClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateToRouteAsync(MainRoute);
    }

    private void OnAgentsNavButtonClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateToRouteAsync(SecondRoute);
    }

    private void OnProvidersNavButtonClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateToRouteAsync(SettingsRoute);
    }

    private async Task NavigateToRouteAsync(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        UpdateNavigationSelection(route);
        var navigator = ContentHost.Navigator() ?? this.Navigator();
        if (navigator is null)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Navigation] Missing navigator for route '{route}'.");
            return;
        }

        var response = await navigator.NavigateRouteAsync(ContentHost, route);
        var success = response?.Success ?? false;
        BrowserConsoleDiagnostics.Info($"[DotPilot.Navigation] Route '{route}' success={success}.");
    }

    private void RegisterContentHostObserver()
    {
        ContentHost.RegisterPropertyChangedCallback(
            ContentControl.ContentProperty,
            (_, _) => UpdateNavigationSelectionFromContent());
    }

    private void UpdateNavigationSelectionFromContent()
    {
        var route = ContentHost.Content switch
        {
            MainPage => MainRoute,
            SecondPage => SecondRoute,
            SettingsPage => SettingsRoute,
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(route))
        {
            var contentTypeName = ContentHost.Content?.GetType().FullName ?? UnknownContentTypeName;
            BrowserConsoleDiagnostics.Info($"[DotPilot.Navigation] Ignoring unrecognized content host type '{contentTypeName}'.");
            return;
        }

        UpdateNavigationSelection(route);
    }

    private void UpdateNavigationSelection(string route)
    {
        _currentRoute = route;
        var selectedStyle = ResolveStyle(SidebarButtonSelectedStyleKey);
        var normalStyle = ResolveStyle(SidebarButtonStyleKey);

        ChatNavButton.Style = string.Equals(_currentRoute, MainRoute, StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
        AgentsNavButton.Style = string.Equals(_currentRoute, SecondRoute, StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
        ProvidersNavButton.Style = string.Equals(_currentRoute, SettingsRoute, StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
    }

    private static Style ResolveStyle(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (Application.Current.Resources.TryGetValue(key, out var style) &&
            style is Style resolvedStyle)
        {
            return resolvedStyle;
        }

        throw new InvalidOperationException($"Unable to resolve style '{key}'.");
    }
}
