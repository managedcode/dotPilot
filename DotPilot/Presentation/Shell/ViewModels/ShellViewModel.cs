using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed class ShellViewModel : ObservableObject
{
    private const string StartupTitleValue = "Preparing local runtime";
    private const string StartupSummaryValue =
        "Loading workspace state and detecting installed CLI providers.";

    private readonly IStartupWorkspaceHydration startupWorkspaceHydration;
    private Microsoft.UI.Xaml.Visibility startupOverlayVisibility = Microsoft.UI.Xaml.Visibility.Visible;

    public ShellViewModel(IStartupWorkspaceHydration startupWorkspaceHydration)
    {
        this.startupWorkspaceHydration = startupWorkspaceHydration;
        this.startupWorkspaceHydration.StateChanged += OnStartupWorkspaceHydrationStateChanged;
        ApplyHydrationState();
    }

    public string StartupTitle => StartupTitleValue;

    public string StartupSummary => StartupSummaryValue;

    public Microsoft.UI.Xaml.Visibility StartupOverlayVisibility
    {
        get => startupOverlayVisibility;
        private set => SetProperty(ref startupOverlayVisibility, value);
    }

    private void OnStartupWorkspaceHydrationStateChanged(object? sender, EventArgs e)
    {
        ApplyHydrationState();
    }

    private void ApplyHydrationState()
    {
        StartupOverlayVisibility = startupWorkspaceHydration.IsReady
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;
    }
}
