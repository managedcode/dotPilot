using DotPilot.Core.ChatSessions;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private const string StartupTitleValue = "Preparing local runtime";
    private const string StartupSummaryValue =
        "Loading workspace state and detecting installed CLI providers.";
    private const string LiveSessionIndicatorTitleValue = "Live session active";
    private const string LiveSessionSummaryFormat = "Running {0} in {1}.";
    private const string SleepPreventionSummaryFormat = "Keeping this machine awake while {0} runs in {1}.";

    private readonly IStartupWorkspaceHydration startupWorkspaceHydration;
    private readonly ISessionActivityMonitor sessionActivityMonitor;
    private readonly DesktopSleepPreventionService desktopSleepPreventionService;
    private readonly UiDispatcher uiDispatcher;
    private Microsoft.UI.Xaml.Visibility startupOverlayVisibility = Microsoft.UI.Xaml.Visibility.Visible;
    private Microsoft.UI.Xaml.Visibility liveSessionIndicatorVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    private string liveSessionIndicatorTitle = string.Empty;
    private string liveSessionIndicatorSummary = string.Empty;

    public ShellViewModel(
        IStartupWorkspaceHydration startupWorkspaceHydration,
        ISessionActivityMonitor sessionActivityMonitor,
        DesktopSleepPreventionService desktopSleepPreventionService,
        UiDispatcher uiDispatcher)
    {
        this.startupWorkspaceHydration = startupWorkspaceHydration;
        this.sessionActivityMonitor = sessionActivityMonitor;
        this.desktopSleepPreventionService = desktopSleepPreventionService;
        this.uiDispatcher = uiDispatcher;
        this.startupWorkspaceHydration.StateChanged += OnStartupWorkspaceHydrationStateChanged;
        this.sessionActivityMonitor.StateChanged += OnSessionActivityStateChanged;
        this.desktopSleepPreventionService.StateChanged += OnDesktopSleepPreventionStateChanged;
        ApplyHydrationState();
        ApplyLiveSessionState();
    }

    public string StartupTitle => StartupTitleValue;

    public string StartupSummary => StartupSummaryValue;

    public Microsoft.UI.Xaml.Visibility StartupOverlayVisibility
    {
        get => startupOverlayVisibility;
        private set => SetProperty(ref startupOverlayVisibility, value);
    }

    public Microsoft.UI.Xaml.Visibility LiveSessionIndicatorVisibility
    {
        get => liveSessionIndicatorVisibility;
        private set => SetProperty(ref liveSessionIndicatorVisibility, value);
    }

    public string LiveSessionIndicatorTitle
    {
        get => liveSessionIndicatorTitle;
        private set => SetProperty(ref liveSessionIndicatorTitle, value);
    }

    public string LiveSessionIndicatorSummary
    {
        get => liveSessionIndicatorSummary;
        private set => SetProperty(ref liveSessionIndicatorSummary, value);
    }

    public void Dispose()
    {
        startupWorkspaceHydration.StateChanged -= OnStartupWorkspaceHydrationStateChanged;
        sessionActivityMonitor.StateChanged -= OnSessionActivityStateChanged;
        desktopSleepPreventionService.StateChanged -= OnDesktopSleepPreventionStateChanged;
    }

    private void OnStartupWorkspaceHydrationStateChanged(object? sender, EventArgs e)
    {
        uiDispatcher.Execute(ApplyHydrationState);
    }

    private void OnSessionActivityStateChanged(object? sender, EventArgs e)
    {
        uiDispatcher.Execute(ApplyLiveSessionState);
    }

    private void OnDesktopSleepPreventionStateChanged(object? sender, EventArgs e)
    {
        uiDispatcher.Execute(ApplyLiveSessionState);
    }

    private void ApplyHydrationState()
    {
        StartupOverlayVisibility = startupWorkspaceHydration.IsReady
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }

    private void ApplyLiveSessionState()
    {
        var snapshot = sessionActivityMonitor.Current;
        if (!snapshot.HasActiveSessions)
        {
            LiveSessionIndicatorVisibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            LiveSessionIndicatorTitle = string.Empty;
            LiveSessionIndicatorSummary = string.Empty;
            return;
        }

        LiveSessionIndicatorVisibility = Microsoft.UI.Xaml.Visibility.Visible;
        LiveSessionIndicatorTitle = LiveSessionIndicatorTitleValue;
        LiveSessionIndicatorSummary = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            desktopSleepPreventionService.IsSleepPreventionActive
                ? SleepPreventionSummaryFormat
                : LiveSessionSummaryFormat,
            snapshot.AgentName,
            snapshot.SessionTitle);
    }
}
