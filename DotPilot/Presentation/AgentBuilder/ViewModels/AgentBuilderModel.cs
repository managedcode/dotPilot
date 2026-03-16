using System.Collections.Immutable;
using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record AgentBuilderModel(
    IAgentWorkspaceState workspaceState,
    AgentPromptDraftGenerator draftGenerator,
    WorkspaceProjectionNotifier workspaceProjectionNotifier,
    ShellNavigationNotifier shellNavigationNotifier,
    ILogger<AgentBuilderModel> logger)
{
    private const string EmptyProviderDisplayName = "Select a provider";
    private const string EmptyProviderStatusSummary =
        "Enable Codex, Claude Code, or GitHub Copilot in Providers before saving the profile.";
    private const string EmptyProviderCommandName = "";
    private const string EmptyModelHelperText =
        "Select an enabled provider to load its supported models.";
    private const string SuggestedModelHelperFormat =
        "Choose one of the supported models for this provider. Suggested: {0}.";
    private const string DraftGenerationProgressMessage = "Generating agent draft...";
    private const string AgentCreationProgressMessage = "Saving local agent profile...";
    private const string PromptGenerationValidationMessage = "Describe the agent you want before generating a draft.";
    private const string AgentValidationMessage = "Enter an agent name before saving the profile.";
    private const string VersionPrefix = "Version ";
    private const string SavedAgentMessageFormat = "Saved {0} using {1}.";
    private const string GeneratedDraftMessageFormat = "Generated draft for {0}. Review and adjust before saving.";
    private const string ManualDraftMessage = "Manual draft ready. Adjust the profile before saving.";
    private const string CatalogTitle = "All agents";
    private const string CatalogSubtitle = "Create, inspect, and reuse local agent profiles.";
    private const string PromptTitle = "New agent";
    private const string PromptSubtitle = "Describe the agent once and review the generated draft.";
    private const string EditorSubtitle = "Review the generated draft and save the profile.";
    private const string EditTitle = "Edit agent";
    private const string EditSubtitle = "Review the current profile and save changes.";
    private const string CreateActionLabel = "Create agent";
    private const string SaveActionLabel = "Save agent";
    private const string SaveChangesActionLabel = "Save changes";
    private const string SessionTitlePrefix = "Session with ";
    private const string StartedChatMessageFormat = "Started a session with {0}.";
    private const string EditingAgentMessageFormat = "Editing {0}. Adjust the profile before saving.";
    private const string UpdatedAgentMessageFormat = "Saved changes to {0} using {1}.";
    private static readonly System.Text.CompositeFormat SavedAgentCompositeFormat =
        System.Text.CompositeFormat.Parse(SavedAgentMessageFormat);
    private static readonly System.Text.CompositeFormat GeneratedDraftCompositeFormat =
        System.Text.CompositeFormat.Parse(GeneratedDraftMessageFormat);
    private static readonly System.Text.CompositeFormat StartedChatCompositeFormat =
        System.Text.CompositeFormat.Parse(StartedChatMessageFormat);
    private static readonly System.Text.CompositeFormat EditingAgentCompositeFormat =
        System.Text.CompositeFormat.Parse(EditingAgentMessageFormat);
    private static readonly System.Text.CompositeFormat UpdatedAgentCompositeFormat =
        System.Text.CompositeFormat.Parse(UpdatedAgentMessageFormat);
    private static readonly System.Text.CompositeFormat SuggestedModelHelperCompositeFormat =
        System.Text.CompositeFormat.Parse(SuggestedModelHelperFormat);
    private static readonly AgentProviderOption EmptySelectedProvider =
        new(AgentProviderKind.Debug, string.Empty, string.Empty, string.Empty, string.Empty, [], null, false);
    private static readonly AgentBuilderSurface CatalogSurface =
        new(AgentBuilderSurfaceKind.Catalog, CatalogTitle, CatalogSubtitle, false, CreateActionLabel);
    private static readonly AgentBuilderSurface PromptSurface =
        new(AgentBuilderSurfaceKind.PromptComposer, PromptTitle, PromptSubtitle, true, string.Empty);
    private static readonly AgentBuilderSurface EditorSurface =
        new(AgentBuilderSurfaceKind.Editor, PromptTitle, EditorSubtitle, true, SaveActionLabel);
    private static readonly AgentBuilderSurface EditSurface =
        new(AgentBuilderSurfaceKind.Editor, EditTitle, EditSubtitle, true, SaveChangesActionLabel);
    private static readonly AgentBuilderView EmptyBuilderView =
        new(
            EmptyProviderDisplayName,
            EmptyProviderStatusSummary,
            EmptyProviderCommandName,
            string.Empty,
            false,
            string.Empty,
            [],
            false,
            EmptyModelHelperText,
            EmptyProviderStatusSummary,
            false);
    private AsyncCommand? _openCreateAgentCommand;
    private AsyncCommand? _returnToCatalogCommand;
    private AsyncCommand? _buildManuallyCommand;
    private AsyncCommand? _generateAgentDraftCommand;
    private AsyncCommand? _saveAgentCommand;
    private AsyncCommand? _startChatForAgentCommand;
    private AsyncCommand? _openEditAgentCommand;
    private AsyncCommand? _providerSelectionChangedCommand;
    private AsyncCommand? _selectModelCommand;
    private readonly Signal _workspaceRefresh = new();

    public IState<AgentBuilderSurface> Surface => State.Value(this, static () => CatalogSurface);

    public IListState<AgentCatalogItem> Agents => ListState.Async(this, LoadAgentsAsync, _workspaceRefresh);

    public IListState<AgentProviderOption> Providers => ListState.Async(this, LoadProvidersAsync, _workspaceRefresh);

    public IState<string> AgentRequest => State.Value(this, static () => string.Empty);

    public IState<string> AgentName => State.Value(this, static () => string.Empty);

    public IState<string> AgentDescription => State.Value(this, static () => string.Empty);

    public IState<string> ModelName => State.Value(this, static () => string.Empty);

    public IState<string> SystemPrompt => State.Value(this, static () => string.Empty);

    public IState<AgentProfileId> EditingAgentId =>
        State.Value(this, static () => default(AgentProfileId));

    public IState<string> OperationMessage => State.Value(this, static () => string.Empty);

    public IState<AgentProviderOption> SelectedProvider => State.Value(this, static () => EmptySelectedProvider);

    public IState<AgentProviderKind> SelectedProviderKind => State.Value(this, static () => AgentProviderKind.Debug);

    public IState<string> BuilderProviderDisplayName => State.Value(this, static () => EmptyProviderDisplayName);

    public IState<string> BuilderProviderStatusSummary => State.Value(this, static () => EmptyProviderStatusSummary);

    public IState<string> BuilderProviderCommandName => State.Value(this, static () => EmptyProviderCommandName);

    public IState<string> BuilderProviderVersionLabel => State.Value(this, static () => string.Empty);

    public IState<bool> BuilderHasProviderVersion => State.Value(this, static () => false);

    public IState<string> BuilderSuggestedModelName => State.Value(this, static () => string.Empty);

    public IState<IReadOnlyList<string>> BuilderSupportedModelNames =>
        State.Value<AgentBuilderModel, IReadOnlyList<string>>(this, static () => Array.Empty<string>());

    public IState<IReadOnlyList<AgentModelOption>> BuilderSupportedModels =>
        State.Value<AgentBuilderModel, IReadOnlyList<AgentModelOption>>(this, static () => Array.Empty<AgentModelOption>());

    public IState<bool> BuilderHasSupportedModels => State.Value(this, static () => false);

    public IState<string> BuilderModelHelperText => State.Value(this, static () => EmptyModelHelperText);

    public IState<string> BuilderStatusMessage => State.Value(this, static () => EmptyProviderStatusSummary);

    public IState<bool> BuilderCanCreateAgent => State.Value(this, static () => false);

    public IState<bool> CanGenerateDraft => State.Async(this, LoadCanGenerateDraftAsync);

    public IState<bool> CanSaveAgent => State.Async(this, LoadCanSaveAgentAsync);

    public IState<AgentBuilderView> Builder => State.Value(this, static () => EmptyBuilderView);

    public ICommand OpenCreateAgentCommand =>
        _openCreateAgentCommand ??= new AsyncCommand(
            () => OpenCreateAgent(CancellationToken.None));

    public ICommand ReturnToCatalogCommand =>
        _returnToCatalogCommand ??= new AsyncCommand(
            () => ReturnToCatalog(CancellationToken.None));

    public ICommand BuildManuallyCommand =>
        _buildManuallyCommand ??= new AsyncCommand(
            () => BuildManually(CancellationToken.None));

    public ICommand GenerateAgentDraftCommand =>
        _generateAgentDraftCommand ??= new AsyncCommand(
            parameter => SubmitAgentDraftCore(parameter as string, CancellationToken.None));

    public ICommand SaveAgentCommand =>
        _saveAgentCommand ??= new AsyncCommand(
            () => SubmitAgentSave(CancellationToken.None));

    public ICommand StartChatForAgentCommand =>
        _startChatForAgentCommand ??= new AsyncCommand(
            parameter => StartChatForParameter(parameter, CancellationToken.None));

    public ICommand OpenEditAgentCommand =>
        _openEditAgentCommand ??= new AsyncCommand(
            parameter => OpenEditAgentForParameter(parameter, CancellationToken.None));

    public ICommand ProviderSelectionChangedCommand =>
        _providerSelectionChangedCommand ??= new AsyncCommand(
            parameter => HandleProviderSelectionChanged(parameter, CancellationToken.None));

    public ICommand SelectModelCommand =>
        _selectModelCommand ??= new AsyncCommand(
            parameter => SelectModel(parameter, CancellationToken.None));

    public async ValueTask OpenCreateAgent(CancellationToken cancellationToken)
    {
        await AgentRequest.SetAsync(string.Empty, cancellationToken);
        await AgentName.SetAsync(string.Empty, cancellationToken);
        await AgentDescription.SetAsync(string.Empty, cancellationToken);
        await ModelName.SetAsync(string.Empty, cancellationToken);
        await SystemPrompt.SetAsync(string.Empty, cancellationToken);
        await EditingAgentId.SetAsync(default, cancellationToken);
        await OperationMessage.SetAsync(string.Empty, cancellationToken);
        await SelectedProvider.UpdateAsync(_ => EmptySelectedProvider, cancellationToken);
        await SelectedProviderKind.UpdateAsync(_ => AgentProviderKind.Debug, cancellationToken);
        await ApplyBuilderViewAsync(EmptyBuilderView, cancellationToken);
        await Surface.UpdateAsync(_ => PromptSurface, cancellationToken);
    }

    public async ValueTask ReturnToCatalog(CancellationToken cancellationToken)
    {
        await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
    }

    public async ValueTask BuildManually(CancellationToken cancellationToken)
    {
        try
        {
            await OperationMessage.SetAsync(ManualDraftMessage, cancellationToken);
            await ApplyDraftAsync(await draftGenerator.CreateManualDraftAsync(cancellationToken), cancellationToken);
            await Surface.UpdateAsync(_ => EditorSurface, cancellationToken);
            AgentBuilderModelLog.ManualDraftCreated(logger);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public ValueTask GenerateAgentDraft(CancellationToken cancellationToken)
    {
        return SubmitAgentDraftCore(promptOverride: null, cancellationToken);
    }

    public ValueTask SubmitAgentDraft(CancellationToken cancellationToken)
    {
        return SubmitAgentDraftCore(promptOverride: null, cancellationToken);
    }

    public async ValueTask HandleSelectedProviderChanged(
        AgentProviderOption? provider,
        CancellationToken cancellationToken)
    {
        await SetSelectedProviderStateAsync(provider, synchronizeModel: true, refreshBuilder: false, cancellationToken);
        await RefreshBuilderAsync(provider, cancellationToken);
    }

    public async ValueTask HandleProviderSelectionChanged(
        object? parameter,
        CancellationToken cancellationToken)
    {
        var provider = parameter switch
        {
            AgentProviderOption option => option,
            AgentProviderKind providerKind => FindProviderByKind(await Providers, providerKind),
            _ => EmptySelectedProvider,
        };

        await HandleSelectedProviderChanged(provider, cancellationToken);
    }

    public async ValueTask SelectModel(object? parameter, CancellationToken cancellationToken)
    {
        var modelName = parameter switch
        {
            AgentModelOption option => option.DisplayName,
            string value => value,
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return;
        }

        await ModelName.UpdateAsync(_ => modelName.Trim(), cancellationToken);
        await RefreshBuilderAsync(cancellationToken);
    }

    private async ValueTask SubmitAgentDraftCore(string? promptOverride, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = string.IsNullOrWhiteSpace(promptOverride)
                ? ((await AgentRequest) ?? string.Empty).Trim()
                : promptOverride.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                await OperationMessage.SetAsync(PromptGenerationValidationMessage, cancellationToken);
                return;
            }

            await AgentRequest.SetAsync(prompt, cancellationToken);
            await OperationMessage.SetAsync(DraftGenerationProgressMessage, cancellationToken);
            AgentBuilderModelLog.DraftGenerationRequested(logger, prompt.Length);
            var draft = await draftGenerator.GenerateAsync(prompt, cancellationToken);
            await ApplyDraftAsync(draft, cancellationToken);
            await OperationMessage.SetAsync(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    GeneratedDraftCompositeFormat,
                    draft.Name),
                cancellationToken);
            await Surface.UpdateAsync(_ => EditorSurface, cancellationToken);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask SaveAgent(CancellationToken cancellationToken)
    {
        try
        {
            var selectedProvider = await ResolveSelectedProviderAsync(cancellationToken);
            if (IsEmptySelectedProvider(selectedProvider))
            {
                await OperationMessage.SetAsync(EmptyProviderStatusSummary, cancellationToken);
                return;
            }

            if (!selectedProvider.CanCreateAgents)
            {
                await OperationMessage.SetAsync(selectedProvider.StatusSummary, cancellationToken);
                return;
            }

            var agentName = ((await AgentName) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(agentName))
            {
                await OperationMessage.SetAsync(AgentValidationMessage, cancellationToken);
                return;
            }

            var modelName = await ResolveEffectiveModelNameAsync(cancellationToken);
            var description = ((await AgentDescription) ?? string.Empty).Trim();
            var systemPrompt = ((await SystemPrompt) ?? string.Empty).Trim();
            var editingAgentId = await EditingAgentId;
            if (editingAgentId != default)
            {
                await OperationMessage.SetAsync(AgentCreationProgressMessage, cancellationToken);
                AgentBuilderModelLog.AgentUpdateRequested(logger, editingAgentId.Value, agentName, selectedProvider.Kind, modelName);

                var updatedResult = await workspaceState.UpdateAgentAsync(
                    new UpdateAgentProfileCommand(
                        editingAgentId,
                        agentName,
                        selectedProvider.Kind,
                        modelName,
                        systemPrompt,
                        description),
                    cancellationToken);
                if (!updatedResult.TryGetValue(out var updated))
                {
                    await OperationMessage.SetAsync(updatedResult.ToOperatorMessage("Could not save the agent."), cancellationToken);
                    return;
                }

                var updatedMessage = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    UpdatedAgentCompositeFormat,
                    updated.Name,
                    updated.ProviderDisplayName);
                _workspaceRefresh.Raise();
                workspaceProjectionNotifier.Publish();
                await EditingAgentId.SetAsync(default, cancellationToken);
                await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
                await OperationMessage.SetAsync(updatedMessage, cancellationToken);
                AgentBuilderModelLog.AgentUpdated(logger, updated.Id.Value, updated.Name, updated.ProviderKind, updated.ModelName);
                return;
            }

            await OperationMessage.SetAsync(AgentCreationProgressMessage, cancellationToken);
            AgentBuilderModelLog.AgentCreationRequested(logger, agentName, selectedProvider.Kind, modelName);

            var createdResult = await workspaceState.CreateAgentAsync(
                new CreateAgentProfileCommand(
                    agentName,
                    selectedProvider.Kind,
                    modelName,
                    systemPrompt,
                    description),
                cancellationToken);
            if (!createdResult.TryGetValue(out var created))
            {
                await OperationMessage.SetAsync(createdResult.ToOperatorMessage("Could not save the agent."), cancellationToken);
                return;
            }

            var savedMessage = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                SavedAgentCompositeFormat,
                created.Name,
                created.ProviderDisplayName);
            _workspaceRefresh.Raise();
            workspaceProjectionNotifier.Publish();
            await OperationMessage.SetAsync(savedMessage, cancellationToken);
            await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
            AgentBuilderModelLog.AgentCreated(logger, created.Id.Value, created.Name, created.ProviderKind, created.ModelName);
            await StartSessionAndOpenChatAsync(created.Id, created.Name, successMessage: null, cancellationToken);
            await OperationMessage.SetAsync(savedMessage, cancellationToken);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public ValueTask SubmitAgentSave(CancellationToken cancellationToken)
    {
        return SaveAgent(cancellationToken);
    }

    public async ValueTask StartChatForAgent(AgentCatalogItem? agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return;
        }

        try
        {
            var startedChatMessage = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                StartedChatCompositeFormat,
                agent.Name);
            AgentBuilderModelLog.ChatSessionRequested(logger, agent.Id.Value, agent.Name);
            await StartSessionAndOpenChatAsync(
                agent.Id,
                agent.Name,
                startedChatMessage,
                cancellationToken);
            await OperationMessage.SetAsync(startedChatMessage, cancellationToken);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private ValueTask StartChatForParameter(object? parameter, CancellationToken cancellationToken)
    {
        return parameter switch
        {
            AgentCatalogItem item => StartChatForAgent(item, cancellationToken),
            AgentCatalogStartChatRequest request => StartChatForRequest(request, cancellationToken),
            _ => ValueTask.CompletedTask,
        };
    }

    private ValueTask OpenEditAgentForParameter(object? parameter, CancellationToken cancellationToken)
    {
        return parameter switch
        {
            AgentCatalogItem item => OpenEditAgent(item.Id, cancellationToken),
            AgentCatalogEditRequest request => OpenEditAgent(request.AgentId, cancellationToken),
            AgentProfileId agentId => OpenEditAgent(agentId, cancellationToken),
            _ => ValueTask.CompletedTask,
        };
    }

    public async ValueTask OpenEditAgent(AgentProfileId agentId, CancellationToken cancellationToken)
    {
        try
        {
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await OperationMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load the agent profile."), cancellationToken);
                return;
            }

            var agent = workspace.Agents.FirstOrDefault(candidate => candidate.Id == agentId);
            if (agent is null)
            {
                await OperationMessage.SetAsync($"Agent '{agentId}' was not found.", cancellationToken);
                return;
            }

            await AgentRequest.SetAsync(string.Empty, cancellationToken);
            await AgentName.SetAsync(agent.Name, cancellationToken);
            await AgentDescription.SetAsync(agent.Description, cancellationToken);
            await SystemPrompt.SetAsync(agent.SystemPrompt, cancellationToken);
            await EditingAgentId.SetAsync(agent.Id, cancellationToken);

            var provider = FindProviderByKind(await Providers, agent.ProviderKind);
            if (IsEmptySelectedProvider(provider))
            {
                provider = await ResolveSelectedProviderAsync(cancellationToken);
            }

            await HandleSelectedProviderChanged(provider, cancellationToken);
            await ModelName.SetAsync(agent.ModelName, cancellationToken);
            await RefreshBuilderAsync(cancellationToken);
            await OperationMessage.SetAsync(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    EditingAgentCompositeFormat,
                    agent.Name),
                cancellationToken);
            await Surface.UpdateAsync(_ => EditSurface, cancellationToken);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask StartChatForRequest(
        AgentCatalogStartChatRequest request,
        CancellationToken cancellationToken)
    {
        var startedChatMessage = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            StartedChatCompositeFormat,
            request.AgentName);
        AgentBuilderModelLog.ChatSessionRequested(logger, request.AgentId.Value, request.AgentName);
        await StartSessionAndOpenChatAsync(
            request.AgentId,
            request.AgentName,
            startedChatMessage,
            cancellationToken);
        await OperationMessage.SetAsync(startedChatMessage, cancellationToken);
    }

    private async ValueTask<IImmutableList<AgentCatalogItem>> LoadAgentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await OperationMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load agents."), cancellationToken);
                return ImmutableArray<AgentCatalogItem>.Empty;
            }

            return workspace.Agents
                .Select(MapAgent)
                .ToImmutableArray();
        }
        catch (TaskCanceledException)
        {
            return ImmutableArray<AgentCatalogItem>.Empty;
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
            return ImmutableArray<AgentCatalogItem>.Empty;
        }
    }

    private async ValueTask<IImmutableList<AgentProviderOption>> LoadProvidersAsync(CancellationToken cancellationToken)
    {
        try
        {
            AgentBuilderModelLog.LoadingProviders(logger);
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await OperationMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load providers."), cancellationToken);
                return ImmutableArray<AgentProviderOption>.Empty;
            }

            AgentBuilderModelLog.ProvidersLoaded(logger, workspace.Providers.Count);

            var providers = workspace.Providers
                .Select(MapProviderOption)
                .Where(static provider => provider.Kind != AgentProviderKind.Debug)
                .ToImmutableArray();

            await EnsureSelectedProviderAsync(providers, cancellationToken);
            return providers;
        }
        catch (TaskCanceledException)
        {
            return ImmutableArray<AgentProviderOption>.Empty;
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
            return ImmutableArray<AgentProviderOption>.Empty;
        }
    }

    private async ValueTask<bool> LoadCanGenerateDraftAsync(CancellationToken cancellationToken)
    {
        var prompt = (await AgentRequest) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(prompt);
    }

    private async ValueTask<bool> LoadCanSaveAgentAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = await ResolveSelectedProviderAsync(cancellationToken);
        var agentName = (await AgentName) ?? string.Empty;
        return !IsEmptySelectedProvider(selectedProvider) &&
            selectedProvider.CanCreateAgents &&
            !string.IsNullOrWhiteSpace(agentName);
    }

    private async ValueTask ApplyDraftAsync(AgentPromptDraft draft, CancellationToken cancellationToken)
    {
        await AgentName.SetAsync(draft.Name, cancellationToken);
        await AgentDescription.SetAsync(draft.Description, cancellationToken);
        await SystemPrompt.SetAsync(draft.SystemPrompt, cancellationToken);

        var providers = await Providers;
        var provider = FindProviderByKind(providers, draft.ProviderKind);
        if (IsEmptySelectedProvider(provider))
        {
            provider = FindFirstCreatableProvider(providers);
        }

        if (IsEmptySelectedProvider(provider) && providers.Count > 0)
        {
            provider = providers[0];
        }

        await HandleSelectedProviderChanged(provider, cancellationToken);
        await ModelName.SetAsync(ResolveDraftModelName(draft, provider), cancellationToken);
        await RefreshBuilderAsync(cancellationToken);
    }

    private async ValueTask EnsureSelectedProviderAsync(
        IImmutableList<AgentProviderOption> providers,
        CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var selectedProviderKind = await SelectedProviderKind;
        var resolvedProvider = IsEmptySelectedProvider(selectedProvider)
            ? EmptySelectedProvider
            : FindProviderByKind(providers, selectedProvider.Kind);

        if (IsEmptySelectedProvider(resolvedProvider))
        {
            resolvedProvider = FindProviderByKind(providers, selectedProviderKind);
        }

        if (IsEmptySelectedProvider(resolvedProvider))
        {
            resolvedProvider = FindFirstCreatableProvider(providers);
        }

        if (IsEmptySelectedProvider(resolvedProvider) && providers.Count > 0)
        {
            resolvedProvider = providers[0];
        }

        if (!Equals(selectedProvider, resolvedProvider) || selectedProviderKind != resolvedProvider.Kind)
        {
            await SetSelectedProviderStateAsync(
                resolvedProvider,
                synchronizeModel: true,
                refreshBuilder: false,
                cancellationToken);
        }
    }

    private async ValueTask SetSelectedProviderStateAsync(
        AgentProviderOption? provider,
        bool synchronizeModel,
        bool refreshBuilder,
        CancellationToken cancellationToken)
    {
        if (provider is null || IsEmptySelectedProvider(provider))
        {
            return;
        }

        var previousProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var currentModelName = (await ModelName) ?? string.Empty;
        var previousSuggestedModel = ResolveSuggestedModelName(previousProvider);
        var nextSuggestedModel = ResolveSuggestedModelName(provider);
        var shouldUpdateModel = synchronizeModel && (
            string.IsNullOrWhiteSpace(currentModelName) ||
            string.Equals(currentModelName, previousSuggestedModel, StringComparison.Ordinal) ||
            !SupportsModel(provider, currentModelName));

        await SelectedProvider.UpdateAsync(_ => provider, cancellationToken);
        await SelectedProviderKind.UpdateAsync(_ => provider.Kind, cancellationToken);
        if (shouldUpdateModel)
        {
            await ModelName.UpdateAsync(_ => nextSuggestedModel, cancellationToken);
        }

        if (refreshBuilder)
        {
            await RefreshBuilderAsync(cancellationToken);
        }
    }

    private async ValueTask RefreshBuilderAsync(CancellationToken cancellationToken)
    {
        var builder = await CreateBuilderViewAsync(providerOverride: null, cancellationToken);
        await ApplyBuilderViewAsync(builder, cancellationToken);
    }

    private async ValueTask RefreshBuilderAsync(
        AgentProviderOption? providerOverride,
        CancellationToken cancellationToken)
    {
        var builder = await CreateBuilderViewAsync(providerOverride, cancellationToken);
        await ApplyBuilderViewAsync(builder, cancellationToken);
    }

    private async ValueTask ApplyBuilderViewAsync(AgentBuilderView builder, CancellationToken cancellationToken)
    {
        await BuilderProviderDisplayName.UpdateAsync(_ => builder.ProviderDisplayName, cancellationToken);
        await BuilderProviderStatusSummary.UpdateAsync(_ => builder.ProviderStatusSummary, cancellationToken);
        await BuilderProviderCommandName.UpdateAsync(_ => builder.ProviderCommandName, cancellationToken);
        await BuilderProviderVersionLabel.UpdateAsync(_ => builder.ProviderVersionLabel, cancellationToken);
        await BuilderHasProviderVersion.UpdateAsync(_ => builder.HasProviderVersion, cancellationToken);
        await BuilderSuggestedModelName.UpdateAsync(_ => builder.SuggestedModelName, cancellationToken);
        await BuilderSupportedModelNames.UpdateAsync(_ => builder.SupportedModelNames, cancellationToken);
        await BuilderSupportedModels.UpdateAsync(_ => builder.SupportedModels, cancellationToken);
        await BuilderHasSupportedModels.UpdateAsync(_ => builder.HasSupportedModels, cancellationToken);
        await BuilderModelHelperText.UpdateAsync(_ => builder.ModelHelperText, cancellationToken);
        await BuilderStatusMessage.UpdateAsync(_ => builder.StatusMessage, cancellationToken);
        await BuilderCanCreateAgent.UpdateAsync(_ => builder.CanCreateAgent, cancellationToken);
        await Builder.UpdateAsync(_ => builder, cancellationToken);
    }

    private async ValueTask<AgentBuilderView> CreateBuilderViewAsync(
        AgentProviderOption? providerOverride,
        CancellationToken cancellationToken)
    {
        var selectedProvider = providerOverride ?? (await SelectedProvider) ?? EmptySelectedProvider;
        if (IsEmptySelectedProvider(selectedProvider))
        {
            selectedProvider = await ResolveSelectedProviderAsync(cancellationToken);
        }

        var suggestedModelName = ResolveSuggestedModelName(selectedProvider);
        var supportedModelNames = ResolveSupportedModelNames(selectedProvider);
        var providerVersionLabel = string.IsNullOrWhiteSpace(selectedProvider.InstalledVersion)
            ? string.Empty
            : VersionPrefix + selectedProvider.InstalledVersion;
        var modelHelperText = string.IsNullOrWhiteSpace(suggestedModelName)
            ? EmptyModelHelperText
            : string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                SuggestedModelHelperCompositeFormat,
                suggestedModelName);

        return new AgentBuilderView(
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderDisplayName : selectedProvider.DisplayName,
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderStatusSummary : selectedProvider.StatusSummary,
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderCommandName : selectedProvider.CommandName,
            providerVersionLabel,
            !string.IsNullOrWhiteSpace(providerVersionLabel),
            suggestedModelName,
            supportedModelNames,
            supportedModelNames.Count > 0,
            modelHelperText,
            ResolveStatusMessage(selectedProvider),
            !IsEmptySelectedProvider(selectedProvider) &&
            selectedProvider.CanCreateAgents &&
            !string.IsNullOrWhiteSpace((await AgentName) ?? string.Empty));
    }

    private async ValueTask<AgentProviderOption> ResolveSelectedProviderAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var providers = await Providers;
        if (!IsEmptySelectedProvider(selectedProvider))
        {
            var resolvedSelectedProvider = FindProviderByKind(providers, selectedProvider.Kind);
            if (!IsEmptySelectedProvider(resolvedSelectedProvider))
            {
                if (!Equals(selectedProvider, resolvedSelectedProvider) ||
                    await SelectedProviderKind != resolvedSelectedProvider.Kind)
                {
                    await SetSelectedProviderStateAsync(
                        resolvedSelectedProvider,
                        synchronizeModel: true,
                        refreshBuilder: false,
                        cancellationToken);
                }

                return resolvedSelectedProvider;
            }
        }

        var selectedProviderKind = await SelectedProviderKind;
        var resolvedProvider = FindProviderByKind(providers, selectedProviderKind);
        if (!IsEmptySelectedProvider(resolvedProvider))
        {
            await SetSelectedProviderStateAsync(
                resolvedProvider,
                synchronizeModel: true,
                refreshBuilder: false,
                cancellationToken);
            return resolvedProvider;
        }

        var creatableProvider = FindFirstCreatableProvider(providers);
        if (!IsEmptySelectedProvider(creatableProvider))
        {
            await SetSelectedProviderStateAsync(
                creatableProvider,
                synchronizeModel: true,
                refreshBuilder: false,
                cancellationToken);
            return creatableProvider;
        }

        if (providers.Count > 0)
        {
            await SetSelectedProviderStateAsync(
                providers[0],
                synchronizeModel: true,
                refreshBuilder: false,
                cancellationToken);
            return providers[0];
        }

        return EmptySelectedProvider;
    }

    private async ValueTask StartSessionAndOpenChatAsync(
        AgentProfileId agentId,
        string agentName,
        string? successMessage,
        CancellationToken cancellationToken)
    {
        var sessionResult = await workspaceState.CreateSessionAsync(
            new CreateSessionCommand(SessionTitlePrefix + agentName, agentId),
            cancellationToken);
        if (sessionResult.IsFailed)
        {
            await OperationMessage.SetAsync(sessionResult.ToOperatorMessage("Could not start a session."), cancellationToken);
            return;
        }

        _workspaceRefresh.Raise();
        workspaceProjectionNotifier.Publish();
        if (!string.IsNullOrWhiteSpace(successMessage))
        {
            await OperationMessage.SetAsync(successMessage, cancellationToken);
        }

        BrowserConsoleDiagnostics.Info(
            $"[DotPilot.AgentBuilder] Session created. AgentId={agentId.Value} AgentName={agentName}. Requesting chat navigation.");
        await TryReturnToCatalogSurfaceAsync(cancellationToken);
        shellNavigationNotifier.Request(ShellRoute.Chat);
    }

    private async ValueTask TryReturnToCatalogSurfaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async ValueTask<string> ResolveEffectiveModelNameAsync(CancellationToken cancellationToken)
    {
        var modelName = ((await ModelName) ?? string.Empty).Trim();
        var selectedProvider = await ResolveSelectedProviderAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(modelName) && SupportsModel(selectedProvider, modelName))
        {
            return modelName;
        }

        return ResolveSuggestedModelName(selectedProvider);
    }

    private AgentCatalogItem MapAgent(AgentProfileSummary agent)
    {
        var automationIdSuffix = CreateAutomationIdSuffix(agent.Name);
        return new AgentCatalogItem(
            agent.Id,
            agent.Name[..1],
            agent.Name,
            agent.Description,
            agent.ProviderDisplayName,
            agent.ModelName,
            AgentSessionDefaults.IsSystemAgent(agent.Name),
            "AgentCatalogEditButton_" + automationIdSuffix,
            new AgentCatalogEditRequest(agent.Id, agent.Name),
            OpenEditAgentCommand,
            "AgentCatalogStartChatButton_" + automationIdSuffix,
            new AgentCatalogStartChatRequest(agent.Id, agent.Name),
            StartChatForAgentCommand);
    }

    private static AgentProviderOption MapProviderOption(ProviderStatusDescriptor provider)
    {
        return new AgentProviderOption(
            provider.Kind,
            provider.DisplayName,
            provider.CommandName,
            provider.StatusSummary,
            provider.SuggestedModelName,
            provider.SupportedModelNames,
            provider.InstalledVersion,
            provider.CanCreateAgents);
    }

    private static AgentProviderOption FindProviderByKind(IImmutableList<AgentProviderOption> providers, AgentProviderKind kind)
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

    private static AgentProviderOption FindFirstCreatableProvider(IImmutableList<AgentProviderOption> providers)
    {
        for (var index = 0; index < providers.Count; index++)
        {
            if (providers[index].CanCreateAgents)
            {
                return providers[index];
            }
        }

        return EmptySelectedProvider;
    }

    private static string ResolveStatusMessage(AgentProviderOption provider)
    {
        return IsEmptySelectedProvider(provider)
            ? EmptyProviderStatusSummary
            : provider.StatusSummary;
    }

    private static string ResolveSuggestedModelName(AgentProviderOption provider)
    {
        return IsEmptySelectedProvider(provider)
            ? string.Empty
            : provider.SuggestedModelName;
    }

    private static List<string> ResolveSupportedModelNames(AgentProviderOption provider)
    {
        if (IsEmptySelectedProvider(provider))
        {
            return [];
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> models = [];
        foreach (var model in new[] { provider.SuggestedModelName }.Concat(provider.SupportedModelNames))
        {
            if (string.IsNullOrWhiteSpace(model) || !seen.Add(model))
            {
                continue;
            }

            models.Add(model);
        }

        return models;
    }

    private static string ResolveDraftModelName(AgentPromptDraft draft, AgentProviderOption provider)
    {
        var suggestedModelName = ResolveSuggestedModelName(provider);
        if (IsEmptySelectedProvider(provider))
        {
            return string.Empty;
        }

        if (provider.Kind != draft.ProviderKind)
        {
            return suggestedModelName;
        }

        return string.IsNullOrWhiteSpace(draft.ModelName) || !SupportsModel(provider, draft.ModelName)
            ? suggestedModelName
            : draft.ModelName;
    }

    private static bool SupportsModel(AgentProviderOption provider, string modelName)
    {
        return ResolveSupportedModelNames(provider)
            .Any(candidate => string.Equals(candidate, modelName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEmptySelectedProvider(AgentProviderOption? provider)
    {
        return provider is null || string.IsNullOrWhiteSpace(provider.DisplayName);
    }

    private static string CreateAutomationIdSuffix(string value)
    {
        var characters = value.Where(char.IsLetterOrDigit).ToArray();
        return characters.Length == 0 ? "Unknown" : new string(characters);
    }

}
