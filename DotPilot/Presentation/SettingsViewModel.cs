using System.Collections.ObjectModel;
using DotPilot.Core.Features.AgentSessions;
using Windows.ApplicationModel.DataTransfer;

namespace DotPilot.Presentation;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly IAgentSessionService _agentSessionService;
    private readonly AsyncCommand _refreshCommand;
    private readonly AsyncCommand _toggleProviderCommand;
    private readonly AsyncCommand _providerActionCommand;
    private readonly ObservableCollection<ProviderStatusItem> _providers;
    private ProviderStatusItem? _selectedProvider;
    private string _statusMessage = string.Empty;

    public SettingsViewModel(IAgentSessionService agentSessionService)
    {
        _agentSessionService = agentSessionService;
        _providers = [];
        _refreshCommand = new AsyncCommand(LoadProvidersAsync);
        _toggleProviderCommand = new AsyncCommand(ToggleSelectedProviderAsync, CanToggleSelectedProvider);
        _providerActionCommand = new AsyncCommand(ExecuteProviderActionAsync, CanExecuteProviderAction);
        _ = LoadProvidersAsync();
    }

    public ObservableCollection<ProviderStatusItem> Providers => _providers;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand ToggleProviderCommand => _toggleProviderCommand;

    public ICommand ProviderActionCommand => _providerActionCommand;

    public string PageTitle => "Providers";

    public string PageSubtitle => "Enable the built-in debug client or connect local CLI providers before creating agents.";

    public ProviderStatusItem? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(SelectedProviderTitle));
            RaisePropertyChanged(nameof(SelectedProviderSummary));
            RaisePropertyChanged(nameof(SelectedProviderInstallCommand));
            RaisePropertyChanged(nameof(SelectedProviderActions));
            RaisePropertyChanged(nameof(HasSelectedProviderActions));
            RaisePropertyChanged(nameof(ToggleActionLabel));
            _toggleProviderCommand.RaiseCanExecuteChanged();
            _providerActionCommand.RaiseCanExecuteChanged();
        }
    }

    public string SelectedProviderTitle => SelectedProvider?.DisplayName ?? "Select a provider";

    public string SelectedProviderSummary => SelectedProvider?.StatusSummary ?? "Choose a provider to inspect readiness and install guidance.";

    public string SelectedProviderInstallCommand => SelectedProvider?.InstallCommand ?? string.Empty;

    public IReadOnlyList<ProviderActionItem> SelectedProviderActions => SelectedProvider?.Actions ?? [];

    public bool HasSelectedProviderActions => SelectedProviderActions.Count > 0;

    public string ToggleActionLabel => SelectedProvider?.IsEnabled == true ? "Disable provider" : "Enable provider";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private async Task LoadProvidersAsync()
    {
        var workspace = await _agentSessionService.GetWorkspaceAsync(CancellationToken.None);
        _providers.Clear();

        foreach (var provider in workspace.Providers)
        {
            _providers.Add(
                new ProviderStatusItem(
                    provider.Kind,
                    provider.DisplayName,
                    provider.CommandName,
                    provider.StatusSummary,
                    provider.InstalledVersion,
                    provider.IsEnabled,
                    provider.CanCreateAgents,
                    provider.Actions.Select(action => action.Command).FirstOrDefault(command => !string.IsNullOrWhiteSpace(command)) ?? string.Empty,
                    provider.Actions
                        .Select(action => new ProviderActionItem(action.Label, action.Summary, action.Command))
                        .ToArray()));
        }

        SelectedProvider = _providers.FirstOrDefault(provider => provider.IsEnabled) ??
            _providers.FirstOrDefault();
    }

    private async Task ToggleSelectedProviderAsync()
    {
        if (SelectedProvider is null)
        {
            return;
        }

        await _agentSessionService.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(SelectedProvider.Kind, !SelectedProvider.IsEnabled),
            CancellationToken.None);
        await LoadProvidersAsync();
        StatusMessage = $"{SelectedProviderTitle} updated.";
    }

    private bool CanToggleSelectedProvider()
    {
        return SelectedProvider is not null;
    }

    private Task ExecuteProviderActionAsync(object? parameter)
    {
        if (parameter is not ProviderActionItem action)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(action.Command))
        {
            StatusMessage = action.Summary;
            return Task.CompletedTask;
        }

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(action.Command);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            StatusMessage = $"Copied command: {action.Command}";
        }
        catch (Exception)
        {
            StatusMessage = $"Run this command in your terminal: {action.Command}";
        }

        return Task.CompletedTask;
    }

    private static bool CanExecuteProviderAction(object? parameter)
    {
        return parameter is ProviderActionItem;
    }
}
