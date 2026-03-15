using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Presentation;

public sealed partial class Shell : Page, IContentControlProvider
{
    private const string SidebarButtonStyleKey = "SidebarButtonStyle";
    private const string SidebarButtonSelectedStyleKey = "SidebarButtonSelectedStyle";
    private const string UnknownContentTypeName = "<null>";
    private ShellNavigationNotifier? _shellNavigationNotifier;
    private string _currentRoute = ResolveRouteName(ShellRoute.Chat);

    public Shell()
    {
        try
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Startup] Shell constructor started.");
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            RegisterContentHostObserver();
            UpdateNavigationSelection(ResolveRouteName(ShellRoute.Chat));
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
        _ = NavigateToRouteAsync(ShellRoute.Chat);
    }

    private void OnAgentsNavButtonClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateToRouteAsync(ShellRoute.Agents);
    }

    private void OnProvidersNavButtonClick(object sender, RoutedEventArgs e)
    {
        _ = NavigateToRouteAsync(ShellRoute.Settings);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ServicesReady -= OnAppServicesReady;
            app.ServicesReady += OnAppServicesReady;
        }

        TryRegisterNavigationNotifier();
    }

    private void TryRegisterNavigationNotifier()
    {
        if (_shellNavigationNotifier is not null)
        {
            return;
        }

        if (Application.Current is not App { Services: { } services })
        {
            BrowserConsoleDiagnostics.Info("[DotPilot.Navigation] Shell navigation notifier is waiting for app services.");
            return;
        }

        _shellNavigationNotifier = services.GetRequiredService<ShellNavigationNotifier>();
        _shellNavigationNotifier.Requested += OnShellNavigationRequested;
        BrowserConsoleDiagnostics.Info("[DotPilot.Navigation] Shell navigation notifier registered.");
    }

    private void OnAppServicesReady(object? sender, EventArgs e)
    {
        TryRegisterNavigationNotifier();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Application.Current is App app)
        {
            app.ServicesReady -= OnAppServicesReady;
        }

        if (_shellNavigationNotifier is null)
        {
            return;
        }

        _shellNavigationNotifier.Requested -= OnShellNavigationRequested;
        _shellNavigationNotifier = null;
    }

    private void OnShellNavigationRequested(object? sender, ShellNavigationRequestedEventArgs e)
    {
        BrowserConsoleDiagnostics.Info($"[DotPilot.Navigation] Shell navigation requested for route '{ResolveRouteName(e.Route)}'.");
        if (DispatcherQueue.HasThreadAccess)
        {
            _ = NavigateToRouteAsync(e.Route);
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => _ = NavigateToRouteAsync(e.Route));
    }

    private async Task NavigateToRouteAsync(ShellRoute route)
    {
        var routeName = ResolveRouteName(route);

        UpdateNavigationSelection(routeName);
        var navigator = ContentHost.Navigator() ?? this.Navigator();
        if (navigator is null)
        {
            BrowserConsoleDiagnostics.Error($"[DotPilot.Navigation] Missing navigator for route '{routeName}'.");
            return;
        }

        var response = await navigator.NavigateRouteAsync(ContentHost, routeName);
        var success = response?.Success ?? false;
        BrowserConsoleDiagnostics.Info($"[DotPilot.Navigation] Route '{routeName}' success={success}.");
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
            ChatPage => ResolveRouteName(ShellRoute.Chat),
            AgentBuilderPage => ResolveRouteName(ShellRoute.Agents),
            SettingsPage => ResolveRouteName(ShellRoute.Settings),
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

        ChatNavButton.Style = string.Equals(_currentRoute, ResolveRouteName(ShellRoute.Chat), StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
        AgentsNavButton.Style = string.Equals(_currentRoute, ResolveRouteName(ShellRoute.Agents), StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
        ProvidersNavButton.Style = string.Equals(_currentRoute, ResolveRouteName(ShellRoute.Settings), StringComparison.Ordinal)
            ? selectedStyle
            : normalStyle;
    }

    private static string ResolveRouteName(ShellRoute route)
    {
        return route switch
        {
            ShellRoute.Chat => "Chat",
            ShellRoute.Agents => "Agents",
            ShellRoute.Settings => "Settings",
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, "Unknown shell route."),
        };
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
