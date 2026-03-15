using System.Collections.Immutable;
using DotPilot.Core.AgentBuilder;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record AgentBuilderModel(
    IAgentWorkspaceState workspaceState,
    AgentPromptDraftGenerator draftGenerator,
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
    private const string CreateActionLabel = "Create agent";
    private const string SaveActionLabel = "Save agent";
    private const string SessionTitlePrefix = "Session with ";
    private const string StartedChatMessageFormat = "Started a session with {0}. Switch to Chat to continue.";
    private static readonly System.Text.CompositeFormat SavedAgentCompositeFormat =
        System.Text.CompositeFormat.Parse(SavedAgentMessageFormat);
    private static readonly System.Text.CompositeFormat GeneratedDraftCompositeFormat =
        System.Text.CompositeFormat.Parse(GeneratedDraftMessageFormat);
    private static readonly System.Text.CompositeFormat StartedChatCompositeFormat =
        System.Text.CompositeFormat.Parse(StartedChatMessageFormat);
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
    private AsyncCommand? _openCreateAgentCommand;
    private AsyncCommand? _returnToCatalogCommand;
    private AsyncCommand? _buildManuallyCommand;
    private AsyncCommand? _generateAgentDraftCommand;
    private AsyncCommand? _saveAgentCommand;
    private AsyncCommand? _startChatForAgentCommand;
    private AsyncCommand? _providerSelectionChangedCommand;
    private readonly Signal _workspaceRefresh = new();

    public IState<AgentBuilderSurface> Surface => State.Value(this, static () => CatalogSurface);

    public IListState<AgentCatalogItem> Agents => ListState.Async(this, LoadAgentsAsync, _workspaceRefresh);

    public IListState<AgentProviderOption> Providers => ListState.Async(this, LoadProvidersAsync, _workspaceRefresh);

    public IState<string> AgentRequest => State.Value(this, static () => string.Empty);

    public IState<string> AgentName => State.Value(this, static () => string.Empty);

    public IState<string> AgentDescription => State.Value(this, static () => string.Empty);

    public IState<string> ModelName => State.Value(this, static () => string.Empty);

    public IState<string> SystemPrompt => State.Value(this, static () => string.Empty);

    public IState<string> OperationMessage => State.Value(this, static () => string.Empty);

    public IState<AgentProviderOption> SelectedProvider => State.Value(this, static () => EmptySelectedProvider);

    public IState<AgentProviderKind> SelectedProviderKind => State.Value(this, static () => AgentProviderKind.Debug);

    public IState<bool> CanGenerateDraft => State.Async(this, LoadCanGenerateDraftAsync);

    public IState<bool> CanSaveAgent => State.Async(this, LoadCanSaveAgentAsync);

    public IState<AgentBuilderView> Builder => State.Async(this, LoadBuilderAsync);

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
            parameter => StartChatForAgent(parameter as AgentCatalogItem, CancellationToken.None));

    public ICommand ProviderSelectionChangedCommand =>
        _providerSelectionChangedCommand ??= new AsyncCommand(
            parameter => HandleSelectedProviderChanged(parameter as AgentProviderOption, CancellationToken.None));

    public async ValueTask OpenCreateAgent(CancellationToken cancellationToken)
    {
        await AgentRequest.SetAsync(string.Empty, cancellationToken);
        await AgentName.SetAsync(string.Empty, cancellationToken);
        await AgentDescription.SetAsync(string.Empty, cancellationToken);
        await ModelName.SetAsync(string.Empty, cancellationToken);
        await SystemPrompt.SetAsync(string.Empty, cancellationToken);
        await OperationMessage.SetAsync(string.Empty, cancellationToken);
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
        if (provider is null || IsEmptySelectedProvider(provider))
        {
            return;
        }

        var previousProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var currentModelName = (await ModelName) ?? string.Empty;
        var previousSuggestedModel = ResolveSuggestedModelName(previousProvider);
        var nextSuggestedModel = ResolveSuggestedModelName(provider);
        var shouldUpdateModel = string.IsNullOrWhiteSpace(currentModelName) ||
            string.Equals(currentModelName, previousSuggestedModel, StringComparison.Ordinal) ||
            !SupportsModel(provider, currentModelName);

        await SelectedProvider.UpdateAsync(_ => provider, cancellationToken);
        await SelectedProviderKind.UpdateAsync(_ => provider.Kind, cancellationToken);
        if (shouldUpdateModel)
        {
            await ModelName.UpdateAsync(_ => nextSuggestedModel, cancellationToken);
        }
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
            await OperationMessage.SetAsync(AgentCreationProgressMessage, cancellationToken);
            AgentBuilderModelLog.AgentCreationRequested(logger, agentName, selectedProvider.Kind, modelName);

            var createdResult = await workspaceState.CreateAgentAsync(
                new CreateAgentProfileCommand(
                    agentName,
                    selectedProvider.Kind,
                    modelName,
                    ((await SystemPrompt) ?? string.Empty).Trim()),
                cancellationToken);
            if (!createdResult.TryGetValue(out var created))
            {
                await OperationMessage.SetAsync(createdResult.ToOperatorMessage("Could not save the agent."), cancellationToken);
                return;
            }

            _workspaceRefresh.Raise();
            await OperationMessage.SetAsync(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    SavedAgentCompositeFormat,
                    created.Name,
                    created.ProviderDisplayName),
                cancellationToken);
            await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
            AgentBuilderModelLog.AgentCreated(logger, created.Id.Value, created.Name, created.ProviderKind, created.ModelName);
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
            AgentBuilderModelLog.ChatSessionRequested(logger, agent.Id.Value, agent.Name);
            var sessionResult = await workspaceState.CreateSessionAsync(
                new CreateSessionCommand(SessionTitlePrefix + agent.Name, agent.Id),
                cancellationToken);
            if (sessionResult.IsFailed)
            {
                await OperationMessage.SetAsync(sessionResult.ToOperatorMessage("Could not start a session."), cancellationToken);
                return;
            }

            _workspaceRefresh.Raise();
            await OperationMessage.SetAsync(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    StartedChatCompositeFormat,
                    agent.Name),
                cancellationToken);
            await Surface.UpdateAsync(_ => CatalogSurface, cancellationToken);
        }
        catch (Exception exception)
        {
            AgentBuilderModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
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

    private async ValueTask<AgentBuilderView> LoadBuilderAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = await ResolveSelectedProviderAsync(cancellationToken);
        var suggestedModelName = ResolveSuggestedModelName(selectedProvider);
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
            ResolveSupportedModelNames(selectedProvider),
            ResolveSupportedModelNames(selectedProvider).Count > 0,
            modelHelperText,
            ResolveStatusMessage(selectedProvider),
            await CanSaveAgent);
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
    }

    private async ValueTask EnsureSelectedProviderAsync(
        IImmutableList<AgentProviderOption> providers,
        CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var selectedProviderKind = await SelectedProviderKind;
        var resolvedProvider = IsEmptySelectedProvider(selectedProvider)
            ? FindProviderByKind(providers, selectedProviderKind)
            : FindProviderByKind(providers, selectedProvider.Kind);
        if (IsEmptySelectedProvider(resolvedProvider))
        {
            resolvedProvider = FindFirstCreatableProvider(providers);
        }

        if (IsEmptySelectedProvider(resolvedProvider) && providers.Count > 0)
        {
            resolvedProvider = providers[0];
        }

        if (!Equals(selectedProvider, resolvedProvider))
        {
            await HandleSelectedProviderChanged(resolvedProvider, cancellationToken);
        }
    }

    private async ValueTask<AgentProviderOption> ResolveSelectedProviderAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        if (!IsEmptySelectedProvider(selectedProvider))
        {
            return selectedProvider;
        }

        var providers = await Providers;
        var selectedProviderKind = await SelectedProviderKind;
        var resolvedByKind = FindProviderByKind(providers, selectedProviderKind);
        if (!IsEmptySelectedProvider(resolvedByKind))
        {
            await HandleSelectedProviderChanged(resolvedByKind, cancellationToken);
            return resolvedByKind;
        }

        var resolvedProvider = FindFirstCreatableProvider(providers);
        if (!IsEmptySelectedProvider(resolvedProvider))
        {
            await HandleSelectedProviderChanged(resolvedProvider, cancellationToken);
            return resolvedProvider;
        }

        if (providers.Count > 0)
        {
            await HandleSelectedProviderChanged(providers[0], cancellationToken);
            return providers[0];
        }

        return EmptySelectedProvider;
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

    private static AgentCatalogItem MapAgent(AgentProfileSummary agent)
    {
        return new AgentCatalogItem(
            agent.Id,
            agent.Name[..1],
            agent.Name,
            AgentSessionDefaults.CreateAgentDescription(agent.SystemPrompt),
            agent.ProviderDisplayName,
            agent.ModelName,
            AgentSessionDefaults.IsSystemAgent(agent.Name));
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

}
