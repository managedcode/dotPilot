using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record SettingsModel
{
    private const string ProvidersSectionKey = "Providers";
    private const string MessagesSectionKey = "Messages";
    private const string ProviderEntryAutomationIdPrefix = "ProviderEntry_";
    private const string RefreshCompletedMessage = "Provider readiness refreshed.";
    private const string ComposerBehaviorSavedMessage = "Message send behavior updated.";
    private const string SelectProviderTitleValue = "Select a provider";
    private const string SelectProviderSummaryValue = "Choose a provider to inspect readiness and install guidance.";
    private const string SettingsTitleValue = "Settings";
    private const string SettingsSubtitleValue = "Tune providers, profile, and message behavior for the local desktop operator.";
    private const string EnableProviderLabel = "Enable provider";
    private const string DisableProviderLabel = "Disable provider";
    private static readonly ProviderStatusItem EmptySelectedProvider = new(
        AgentProviderKind.Debug,
        string.Empty,
        string.Empty,
        string.Empty,
        null,
        false,
        false,
        [],
        [],
        string.Empty,
        false);

    private readonly IAgentWorkspaceState workspaceState;
    private readonly IOperatorPreferencesStore operatorPreferencesStore;
    private readonly ILocalModelPathPicker localModelPathPicker;
    private readonly WorkspaceProjectionNotifier workspaceProjectionNotifier;
    private readonly ILogger<SettingsModel> logger;
    private AsyncCommand? _refreshCommand;
    private AsyncCommand? _toggleSelectedProviderCommand;
    private AsyncCommand? _selectProviderCommand;
    private AsyncCommand? _executeProviderActionCommand;
    private AsyncCommand? _selectSectionCommand;
    private AsyncCommand? _selectComposerSendBehaviorCommand;
    private readonly Signal _workspaceRefresh = new();

    public SettingsModel(
        IAgentWorkspaceState workspaceState,
        IOperatorPreferencesStore operatorPreferencesStore,
        ILocalModelPathPicker localModelPathPicker,
        WorkspaceProjectionNotifier workspaceProjectionNotifier,
        ILogger<SettingsModel> logger)
    {
        this.workspaceState = workspaceState;
        this.operatorPreferencesStore = operatorPreferencesStore;
        this.localModelPathPicker = localModelPathPicker;
        this.workspaceProjectionNotifier = workspaceProjectionNotifier;
        this.logger = logger;
        workspaceProjectionNotifier.Changed += OnWorkspaceProjectionChanged;
    }

    public string PageTitle => SettingsTitleValue;

    public string PageSubtitle => SettingsSubtitleValue;

    public string EnterSendsOptionTitle => ChatComposerSendBehaviorText.GetTitle(ComposerSendBehavior.EnterSends);

    public string EnterSendsOptionSummary => ChatComposerSendBehaviorText.GetSummary(ComposerSendBehavior.EnterSends);

    public string EnterInsertsNewLineOptionTitle => ChatComposerSendBehaviorText.GetTitle(ComposerSendBehavior.EnterInsertsNewLine);

    public string EnterInsertsNewLineOptionSummary => ChatComposerSendBehaviorText.GetSummary(ComposerSendBehavior.EnterInsertsNewLine);

    public IListState<ProviderStatusItem> Providers => ListState.Async(this, LoadProvidersAsync, _workspaceRefresh);

    public IState<SettingsSection> SelectedSection => State.Value(this, static () => SettingsSection.Providers);

    public IState<bool> IsProvidersSectionSelected => State.Value(this, static () => true);

    public IState<bool> IsMessagesSectionSelected => State.Value(this, static () => false);

    public IState<bool> IsProfileSectionSelected => State.Value(this, static () => false);

    public IState<bool> ShowProvidersSection => State.Value(this, static () => true);

    public IState<bool> ShowMessagesSection => State.Value(this, static () => false);

    public IState<bool> ShowProfileSection => State.Value(this, static () => false);

    public IState<ProviderStatusItem> SelectedProvider => State.Value(this, static () => EmptySelectedProvider);

    public IState<string> SelectedProviderTitle => State.Value(this, static () => SelectProviderTitleValue);

    public IState<string> SelectedProviderSummary => State.Value(this, static () => SelectProviderSummaryValue);

    public IState<string> SelectedProviderCommandName => State.Value(this, static () => string.Empty);

    public IState<ImmutableArray<ProviderDetailItem>> SelectedProviderDetails => State.Value(this, static () => ImmutableArray<ProviderDetailItem>.Empty);

    public IState<bool> HasSelectedProviderDetails => State.Value(this, static () => false);

    public IState<ImmutableArray<ProviderActionItem>> SelectedProviderActions => State.Value(this, static () => ImmutableArray<ProviderActionItem>.Empty);

    public IState<bool> HasSelectedProviderActions => State.Value(this, static () => false);

    public IState<string> ToggleActionLabel => State.Value(this, static () => EnableProviderLabel);

    public IState<string> StatusMessage => State.Value(this, static () => string.Empty);

    public IState<bool> CanToggleSelectedProvider => State.Value(this, static () => false);

    public IState<ComposerSendBehavior> SelectedComposerSendBehavior => State.Value(this, static () => ComposerSendBehavior.EnterSends);

    public IState<string> ComposerSendBehaviorHint => State.Value(
        this,
        static () => ChatComposerSendBehaviorText.GetHint(ComposerSendBehavior.EnterSends));

    public IState<bool> IsEnterSendsSelected => State.Value(this, static () => true);

    public IState<bool> IsEnterInsertsNewLineSelected => State.Value(this, static () => false);

    public ICommand RefreshCommand =>
        _refreshCommand ??= new AsyncCommand(
            () => Refresh(CancellationToken.None));

    public ICommand ToggleSelectedProviderCommand =>
        _toggleSelectedProviderCommand ??= new AsyncCommand(
            () => ToggleSelectedProvider(CancellationToken.None));

    public ICommand SelectProviderCommand =>
        _selectProviderCommand ??= new AsyncCommand(
            parameter => SelectProvider(parameter as ProviderStatusItem, CancellationToken.None));

    public ICommand ExecuteProviderActionCommand =>
        _executeProviderActionCommand ??= new AsyncCommand(
            parameter => ExecuteProviderAction(parameter as ProviderActionItem, CancellationToken.None));

    public ICommand SelectSectionCommand =>
        _selectSectionCommand ??= new AsyncCommand(
            parameter => SelectSection(parameter as string, CancellationToken.None));

    public ICommand SelectComposerSendBehaviorCommand =>
        _selectComposerSendBehaviorCommand ??= new AsyncCommand(
            parameter => SelectComposerSendBehavior(parameter as string, CancellationToken.None));

    public async ValueTask Refresh(CancellationToken cancellationToken)
    {
        try
        {
            SettingsModelLog.RefreshRequested(logger);
            var workspaceResult = await workspaceState.RefreshWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await StatusMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not refresh provider readiness."), cancellationToken);
                return;
            }

            var providers = MapProviderStatusItems(workspace.Providers, selectedProvider: null);
            await EnsureSelectedProviderAsync(workspace, providers, cancellationToken);
            _workspaceRefresh.Raise();
            await StatusMessage.SetAsync(RefreshCompletedMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask ToggleSelectedProvider(CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        if (IsEmptySelectedProvider(selectedProvider))
        {
            return;
        }

        try
        {
            var currentSelectedProvider = await SelectedProvider ?? EmptySelectedProvider;
            var updatedResult = await workspaceState.UpdateProviderAsync(
                new UpdateProviderPreferenceCommand(currentSelectedProvider.Kind, !currentSelectedProvider.IsEnabled),
                cancellationToken);
            if (!updatedResult.TryGetValue(out var updated))
            {
                await StatusMessage.SetAsync(updatedResult.ToOperatorMessage("Could not update provider state."), cancellationToken);
                return;
            }

            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await StatusMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not reload workspace."), cancellationToken);
                return;
            }

            var providers = MapProviderStatusItems(workspace.Providers, selectedProvider: null);
            await EnsureSelectedProviderAsync(workspace, providers, cancellationToken);
            _workspaceRefresh.Raise();
            await StatusMessage.SetAsync($"{updated.DisplayName} updated.", cancellationToken);
            workspaceProjectionNotifier.Publish();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask SelectProvider(ProviderStatusItem? provider, CancellationToken cancellationToken)
    {
        if (provider is null || IsEmptySelectedProvider(provider))
        {
            return;
        }

        await SetSelectedProviderAsync(provider, cancellationToken);
        _workspaceRefresh.Raise();
    }

    public async ValueTask SelectSection(string? sectionKey, CancellationToken cancellationToken)
    {
        if (!TryParseSection(sectionKey, out var section))
        {
            return;
        }

        await SetSelectedSectionAsync(section, cancellationToken);
    }

    public async ValueTask SelectComposerSendBehavior(string? behaviorKey, CancellationToken cancellationToken)
    {
        if (!TryParseComposerSendBehavior(behaviorKey, out var behavior))
        {
            return;
        }

        try
        {
            var preferences = await operatorPreferencesStore.SetAsync(behavior, cancellationToken);
            await SynchronizeComposerSendBehaviorAsync(preferences, cancellationToken);
            await StatusMessage.SetAsync(ComposerBehaviorSavedMessage, cancellationToken);
            _workspaceRefresh.Raise();
            workspaceProjectionNotifier.Publish();
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask<IImmutableList<ProviderStatusItem>> LoadProvidersAsync(CancellationToken cancellationToken)
    {
        try
        {
            SettingsModelLog.LoadingProviders(logger);
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await StatusMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load providers."), cancellationToken);
                return ImmutableArray<ProviderStatusItem>.Empty;
            }

            SettingsModelLog.ProvidersLoaded(logger, workspace.Providers.Count);
            var providers = MapProviderStatusItems(workspace.Providers, selectedProvider: (await SelectedProvider) ?? EmptySelectedProvider);
            var selectedProvider = await EnsureSelectedProviderAsync(workspace, providers, cancellationToken);
            return MapProviderStatusItems(workspace.Providers, selectedProvider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ImmutableArray<ProviderStatusItem>.Empty;
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
            return ImmutableArray<ProviderStatusItem>.Empty;
        }
    }

    private async ValueTask<ProviderStatusItem> EnsureSelectedProviderAsync(
        AgentWorkspaceSnapshot workspace,
        IImmutableList<ProviderStatusItem> providers,
        CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var resolvedProvider = IsEmptySelectedProvider(selectedProvider)
            ? EmptySelectedProvider
            : FindProviderByKind(providers, selectedProvider.Kind);

        if (IsEmptySelectedProvider(resolvedProvider))
        {
            var enabledProviderKind = FindEnabledProviderKind(workspace.Providers);
            if (enabledProviderKind is { } providerKind)
            {
                resolvedProvider = FindProviderByKind(providers, providerKind);
            }
        }

        if (IsEmptySelectedProvider(resolvedProvider) && providers.Count > 0)
        {
            resolvedProvider = providers[0];
        }

        await SetSelectedProviderAsync(resolvedProvider, cancellationToken);
        return resolvedProvider;
    }

    private async ValueTask SetSelectedProviderAsync(
        ProviderStatusItem selectedProvider,
        CancellationToken cancellationToken)
    {
        var currentProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        if (!AreSameProvider(currentProvider, selectedProvider))
        {
            await SelectedProvider.UpdateAsync(_ => selectedProvider, cancellationToken);
        }

        if (!IsEmptySelectedProvider(selectedProvider))
        {
            SettingsModelLog.ProviderSelected(logger, selectedProvider.Kind, selectedProvider.DisplayName);
        }

        await SynchronizeSelectedProviderProjectionAsync(selectedProvider, cancellationToken);
    }

    private async ValueTask SynchronizeSelectedProviderProjectionAsync(
        ProviderStatusItem? selectedProvider,
        CancellationToken cancellationToken)
    {
        selectedProvider ??= EmptySelectedProvider;
        var actions = IsEmptySelectedProvider(selectedProvider)
            ? ImmutableArray<ProviderActionItem>.Empty
            : selectedProvider.Actions.ToImmutableArray();
        var details = IsEmptySelectedProvider(selectedProvider)
            ? ImmutableArray<ProviderDetailItem>.Empty
            : selectedProvider.Details.ToImmutableArray();

        await SelectedProviderTitle.SetAsync(
            IsEmptySelectedProvider(selectedProvider) ? SelectProviderTitleValue : selectedProvider.DisplayName,
            cancellationToken);
        await SelectedProviderSummary.SetAsync(
            IsEmptySelectedProvider(selectedProvider) ? SelectProviderSummaryValue : selectedProvider.StatusSummary,
            cancellationToken);
        await SelectedProviderCommandName.SetAsync(
            IsEmptySelectedProvider(selectedProvider) ? string.Empty : selectedProvider.CommandName,
            cancellationToken);
        await SelectedProviderDetails.UpdateAsync(_ => details, cancellationToken);
        await HasSelectedProviderDetails.UpdateAsync(_ => details.Length > 0, cancellationToken);
        await SelectedProviderActions.UpdateAsync(_ => actions, cancellationToken);
        await HasSelectedProviderActions.UpdateAsync(_ => actions.Length > 0, cancellationToken);
        await ToggleActionLabel.SetAsync(
            !IsEmptySelectedProvider(selectedProvider) && selectedProvider.IsEnabled
                ? DisableProviderLabel
                : EnableProviderLabel,
            cancellationToken);
        await CanToggleSelectedProvider.UpdateAsync(_ => !IsEmptySelectedProvider(selectedProvider), cancellationToken);
    }

    private void OnWorkspaceProjectionChanged(object? sender, EventArgs e)
    {
        _workspaceRefresh.Raise();
    }

    private async ValueTask SetSelectedSectionAsync(SettingsSection section, CancellationToken cancellationToken)
    {
        await SelectedSection.SetAsync(section, cancellationToken);
        await IsProvidersSectionSelected.SetAsync(section is SettingsSection.Providers, cancellationToken);
        await IsMessagesSectionSelected.SetAsync(section is SettingsSection.Messages, cancellationToken);
        await IsProfileSectionSelected.SetAsync(section is SettingsSection.Profile, cancellationToken);
        await ShowProvidersSection.SetAsync(section is SettingsSection.Providers, cancellationToken);
        await ShowMessagesSection.SetAsync(section is SettingsSection.Messages, cancellationToken);
        await ShowProfileSection.SetAsync(section is SettingsSection.Profile, cancellationToken);
        if (section is not SettingsSection.Profile)
        {
            await ClearDeleteAllDataConfirmationAsync(cancellationToken);
        }

        if (section is SettingsSection.Messages)
        {
            await SynchronizeComposerSendBehaviorAsync(await operatorPreferencesStore.GetAsync(cancellationToken), cancellationToken);
        }
    }

    private async ValueTask SynchronizeComposerSendBehaviorAsync(
        OperatorPreferencesSnapshot preferences,
        CancellationToken cancellationToken)
    {
        await SelectedComposerSendBehavior.SetAsync(preferences.ComposerSendBehavior, cancellationToken);
        await ComposerSendBehaviorHint.SetAsync(
            ChatComposerSendBehaviorText.GetHint(preferences.ComposerSendBehavior),
            cancellationToken);
        await IsEnterSendsSelected.SetAsync(
            preferences.ComposerSendBehavior is ComposerSendBehavior.EnterSends,
            cancellationToken);
        await IsEnterInsertsNewLineSelected.SetAsync(
            preferences.ComposerSendBehavior is ComposerSendBehavior.EnterInsertsNewLine,
            cancellationToken);
    }

    private static bool TryParseSection(string? sectionKey, out SettingsSection section)
    {
        if (string.Equals(sectionKey, ProvidersSectionKey, StringComparison.Ordinal))
        {
            section = SettingsSection.Providers;
            return true;
        }

        if (string.Equals(sectionKey, MessagesSectionKey, StringComparison.Ordinal))
        {
            section = SettingsSection.Messages;
            return true;
        }

        if (string.Equals(sectionKey, ProfileSectionKey, StringComparison.Ordinal))
        {
            section = SettingsSection.Profile;
            return true;
        }

        section = SettingsSection.Providers;
        return false;
    }

    private static bool TryParseComposerSendBehavior(string? behaviorKey, out ComposerSendBehavior behavior)
    {
        if (Enum.TryParse(behaviorKey, ignoreCase: true, out behavior))
        {
            return true;
        }

        behavior = ComposerSendBehavior.EnterSends;
        return false;
    }

    private static AgentProviderKind? FindEnabledProviderKind(IReadOnlyList<ProviderStatusDescriptor> providers)
    {
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index].IsEnabled && IsVisibleProvider(providers[index].Kind))
            {
                return providers[index].Kind;
            }
        }

        return null;
    }

    private static ProviderStatusItem FindProviderByKind(IImmutableList<ProviderStatusItem> providers, AgentProviderKind kind)
    {
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index].Kind == kind)
            {
                return providers[index];
            }
        }

        return EmptySelectedProvider;
    }

    private static bool IsEmptySelectedProvider(ProviderStatusItem provider)
    {
        return string.IsNullOrWhiteSpace(provider.DisplayName);
    }

    private static bool AreSameProvider(ProviderStatusItem left, ProviderStatusItem right)
    {
        return left.Kind == right.Kind &&
            string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
            string.Equals(left.CommandName, right.CommandName, StringComparison.Ordinal) &&
            string.Equals(left.StatusSummary, right.StatusSummary, StringComparison.Ordinal) &&
            left.IsEnabled == right.IsEnabled &&
            left.CanCreateAgents == right.CanCreateAgents &&
            HaveSameDetails(left.Details, right.Details) &&
            HaveSameActions(left.Actions, right.Actions);
    }

    private static ImmutableArray<ProviderStatusItem> MapProviderStatusItems(
        IReadOnlyList<ProviderStatusDescriptor> providers,
        ProviderStatusItem? selectedProvider)
    {
        return providers
            .Where(static provider => IsVisibleProvider(provider.Kind))
            .Select(provider => MapProviderStatusItem(provider, selectedProvider))
            .ToImmutableArray();
    }

    private static ProviderStatusItem MapProviderStatusItem(
        ProviderStatusDescriptor provider,
        ProviderStatusItem? selectedProvider)
    {
        return new ProviderStatusItem(
            provider.Kind,
            provider.DisplayName,
            provider.CommandName,
            provider.StatusSummary,
            provider.InstalledVersion,
            provider.IsEnabled,
            provider.CanCreateAgents,
            provider.Details
                .Select(detail => new ProviderDetailItem(detail.Label, detail.Value))
                .ToArray(),
            provider.Actions
                .Select(action => new ProviderActionItem(action.Label, action.Summary, action.Command, action.Kind))
                .ToArray(),
            ProviderEntryAutomationIdPrefix + provider.Kind,
            selectedProvider is not null &&
            !IsEmptySelectedProvider(selectedProvider) &&
            selectedProvider.Kind == provider.Kind);
    }

    private static bool HaveSameDetails(
        IReadOnlyList<ProviderDetailItem> left,
        IReadOnlyList<ProviderDetailItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Label, right[index].Label, StringComparison.Ordinal) ||
                !string.Equals(left[index].Value, right[index].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HaveSameActions(
        IReadOnlyList<ProviderActionItem> left,
        IReadOnlyList<ProviderActionItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Label, right[index].Label, StringComparison.Ordinal) ||
                !string.Equals(left[index].Summary, right[index].Summary, StringComparison.Ordinal) ||
                !string.Equals(left[index].Command, right[index].Command, StringComparison.Ordinal) ||
                left[index].Kind != right[index].Kind)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVisibleProvider(AgentProviderKind kind)
    {
        return kind is AgentProviderKind.Codex or AgentProviderKind.ClaudeCode or AgentProviderKind.GitHubCopilot or AgentProviderKind.Gemini or AgentProviderKind.Onnx or AgentProviderKind.LlamaSharp;
    }
}
