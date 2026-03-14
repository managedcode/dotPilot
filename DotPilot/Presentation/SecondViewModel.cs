using System.Collections.Immutable;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record SecondModel(
    IAgentWorkspaceState workspaceState,
    ILogger<SecondModel> logger)
{
    private const string AgentValidationMessage = "Enter an agent name before creating the profile.";
    private const string AgentCreationProgressMessage = "Creating agent profile...";
    private const string DefaultAgentName = "Debug Agent";
    private const string EmptyProviderDisplayName = "No provider selected";
    private const string EmptyProviderStatusSummary = "Provider readiness is still loading.";
    private const string EmptyProviderCommandName = "No command available";
    private const string ReadyMessageFormat = "Ready to create an agent with {0}.";
    private const string VersionPrefix = "Version ";
    private static readonly System.Text.CompositeFormat ReadyMessageCompositeFormat =
        System.Text.CompositeFormat.Parse(ReadyMessageFormat);
    private const string DefaultSystemPrompt =
        "You are a helpful local desktop agent. Be explicit about what you are doing and stream visible progress.";
    private static readonly AgentProviderOption EmptySelectedProvider =
        new(AgentProviderKind.Debug, string.Empty, string.Empty, string.Empty, null, false);
    private static readonly RoleOption DefaultRole = new("Assistant", AgentRoleKind.Operator);
    private readonly Signal _workspaceRefresh = new();

    public string PageTitle => "Create agent";

    public string PageSubtitle => "Configure a reusable local agent profile for sessions and workflows.";

    public IState<string> AgentName => State.Value(this, static () => DefaultAgentName);

    public IState<string> ModelName => State.Value(this, static () => string.Empty);

    public IState<string> SystemPrompt => State.Value(this, static () => DefaultSystemPrompt);

    public IState<string> OperationMessage => State.Value(this, static () => string.Empty);

    public IListState<AgentProviderOption> Providers => ListState.Async(this, LoadProvidersAsync, _workspaceRefresh);

    public IState<AgentProviderOption> SelectedProvider => State.Value(this, static () => EmptySelectedProvider);

    public IState<RoleOption> SelectedRole => State.Value(this, static () => DefaultRole);

    public IImmutableList<RoleOption> Roles { get; } =
    [
        DefaultRole,
        new RoleOption("Coder", AgentRoleKind.Coding),
        new RoleOption("Researcher", AgentRoleKind.Research),
        new RoleOption("Reviewer", AgentRoleKind.Reviewer),
    ];

    public IImmutableList<CapabilityOption> Capabilities { get; } =
    [
        new CapabilityOption("Web", "Web research and browsing workflows.", true),
        new CapabilityOption("Shell", "Terminal-style command execution.", true),
        new CapabilityOption("Git", "Repository status, diff, and branch operations.", true),
        new CapabilityOption("Files", "Read and update local files.", true),
    ];

    public IState<bool> CanCreateAgent => State.Async(this, LoadCanCreateAgentAsync);

    public IState<AgentBuilderView> Builder => State.Async(this, LoadBuilderAsync);

    public async ValueTask CreateAgent(CancellationToken cancellationToken)
    {
        try
        {
            var selectedProvider = await ResolveSelectedProviderAsync();
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

            var modelName = await ResolveEffectiveModelNameAsync();
            await OperationMessage.SetAsync(AgentCreationProgressMessage, cancellationToken);
            SecondViewModelLog.AgentCreationRequested(logger, agentName, selectedProvider.Kind, modelName);
            BrowserConsoleDiagnostics.Info(
                $"[DotPilot.AgentBuilder] Create requested. Name={agentName} Provider={selectedProvider.Kind} Model={modelName}");

            var created = await workspaceState.CreateAgentAsync(
                new CreateAgentProfileCommand(
                    agentName,
                    ((await SelectedRole) ?? DefaultRole).Role,
                    selectedProvider.Kind,
                    modelName,
                    ((await SystemPrompt) ?? string.Empty).Trim(),
                    Capabilities
                        .Where(option => option.IsEnabled)
                        .Select(option => option.Name)
                        .ToArray()),
                cancellationToken);

            _workspaceRefresh.Raise();
            SecondViewModelLog.AgentCreated(logger, created.Id.Value, created.Name, created.ProviderKind, created.ModelName);
            await OperationMessage.SetAsync($"Created {created.Name} using {created.ProviderDisplayName}.", cancellationToken);
            BrowserConsoleDiagnostics.Info(
                $"[DotPilot.AgentBuilder] Create completed. AgentId={created.Id.Value:N} Name={created.Name} Provider={created.ProviderKind} Model={created.ModelName}");
        }
        catch (Exception exception)
        {
            SecondViewModelLog.Failure(logger, exception);
            BrowserConsoleDiagnostics.Error($"[DotPilot.AgentBuilder] Create failed. {exception}");
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask<IImmutableList<AgentProviderOption>> LoadProvidersAsync(CancellationToken cancellationToken)
    {
        try
        {
            SecondViewModelLog.LoadingProviders(logger);
            var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
            SecondViewModelLog.ProvidersLoaded(logger, workspace.Providers.Count);

            var providers = workspace.Providers
                .Select(MapProviderOption)
                .ToImmutableArray();

            await EnsureSelectedProviderAsync(providers, cancellationToken);
            return providers;
        }
        catch (Exception exception)
        {
            SecondViewModelLog.Failure(logger, exception);
            await OperationMessage.SetAsync(exception.Message, cancellationToken);
            return ImmutableArray<AgentProviderOption>.Empty;
        }
    }

    private async ValueTask<bool> LoadCanCreateAgentAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = await ResolveSelectedProviderAsync();
        var agentName = (await AgentName) ?? string.Empty;
        return !IsEmptySelectedProvider(selectedProvider) &&
            selectedProvider.CanCreateAgents &&
            !string.IsNullOrWhiteSpace(agentName);
    }

    private async ValueTask<AgentBuilderView> LoadBuilderAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = await ResolveSelectedProviderAsync();
        var suggestedModelName = ResolveSuggestedModelName(selectedProvider);
        var providerVersionLabel = string.IsNullOrWhiteSpace(selectedProvider.InstalledVersion)
            ? string.Empty
            : VersionPrefix + selectedProvider.InstalledVersion;

        return new AgentBuilderView(
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderDisplayName : selectedProvider.DisplayName,
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderStatusSummary : selectedProvider.StatusSummary,
            IsEmptySelectedProvider(selectedProvider) ? EmptyProviderCommandName : selectedProvider.CommandName,
            providerVersionLabel,
            !string.IsNullOrWhiteSpace(providerVersionLabel),
            suggestedModelName,
            ResolveStatusMessage(selectedProvider),
            await CanCreateAgent);
    }

    private async ValueTask EnsureSelectedProviderAsync(
        IImmutableList<AgentProviderOption> providers,
        CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        var resolvedProvider = FindProviderByKind(providers, selectedProvider.Kind);
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
            await SelectedProvider.UpdateAsync(_ => resolvedProvider, cancellationToken);
        }
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

    private static bool IsEmptySelectedProvider(AgentProviderOption? provider)
    {
        return provider is null || string.IsNullOrWhiteSpace(provider.DisplayName);
    }

    private async ValueTask<AgentProviderOption> ResolveSelectedProviderAsync()
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        if (!IsEmptySelectedProvider(selectedProvider))
        {
            return selectedProvider;
        }

        var providers = await Providers;
        var resolvedProvider = FindFirstCreatableProvider(providers);
        if (!IsEmptySelectedProvider(resolvedProvider))
        {
            return resolvedProvider;
        }

        return providers.Count > 0 ? providers[0] : EmptySelectedProvider;
    }

    private async ValueTask<string> ResolveEffectiveModelNameAsync()
    {
        var modelName = ((await ModelName) ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            return modelName;
        }

        return ResolveSuggestedModelName(await ResolveSelectedProviderAsync());
    }

    private static AgentProviderOption MapProviderOption(ProviderStatusDescriptor provider)
    {
        return new AgentProviderOption(
            provider.Kind,
            provider.DisplayName,
            provider.CommandName,
            provider.StatusSummary,
            provider.InstalledVersion,
            provider.CanCreateAgents);
    }

    private static string ResolveStatusMessage(AgentProviderOption provider)
    {
        if (IsEmptySelectedProvider(provider))
        {
            return EmptyProviderStatusSummary;
        }

        return provider.CanCreateAgents
            ? string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                ReadyMessageCompositeFormat,
                provider.DisplayName)
            : provider.StatusSummary;
    }

    private static string ResolveSuggestedModelName(AgentProviderOption provider)
    {
        return IsEmptySelectedProvider(provider)
            ? ResolveDefaultModel(AgentProviderKind.Debug)
            : ResolveDefaultModel(provider.Kind);
    }

    private static string ResolveDefaultModel(AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Codex => "gpt-5",
            AgentProviderKind.ClaudeCode => "claude-sonnet-4-5",
            AgentProviderKind.GitHubCopilot => "gpt-5",
            _ => "debug-echo",
        };
    }
}
