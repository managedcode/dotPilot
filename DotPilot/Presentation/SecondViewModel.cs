using System.Collections.ObjectModel;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Presentation;

public sealed class SecondViewModel : ObservableObject
{
    private const string AgentValidationMessage = "Enter an agent name and model before creating the profile.";
    private const string AgentCreationProgressMessage = "Creating agent profile...";
    private readonly IAgentSessionService _agentSessionService;
    private readonly AsyncCommand _createAgentCommand;
    private readonly ObservableCollection<RoleOption> _roleOptions;
    private readonly ObservableCollection<CapabilityOption> _capabilityOptions;
    private readonly ObservableCollection<AgentProviderOption> _providerOptions;
    private string _agentName = "Debug Agent";
    private string _modelName = "debug-echo";
    private string _systemPrompt =
        "You are a helpful local desktop agent. Be explicit about what you are doing and stream visible progress.";
    private string _statusMessage = "Loading provider readiness...";
    private AgentProviderOption? _selectedProvider;

    public SecondViewModel(IAgentSessionService agentSessionService)
    {
        _agentSessionService = agentSessionService;
        _providerOptions = [];
        _roleOptions =
        [
            new RoleOption("Assistant", AgentRoleKind.Operator, true),
            new RoleOption("Coder", AgentRoleKind.Coding, false),
            new RoleOption("Researcher", AgentRoleKind.Research, false),
            new RoleOption("Reviewer", AgentRoleKind.Reviewer, false),
        ];
        _capabilityOptions =
        [
            new CapabilityOption("Web", "Web research and browsing workflows.", true),
            new CapabilityOption("Shell", "Terminal-style command execution.", true),
            new CapabilityOption("Git", "Repository status, diff, and branch operations.", true),
            new CapabilityOption("Files", "Read and update local files.", true),
        ];

        _createAgentCommand = new AsyncCommand(CreateAgentAsync);
        _ = LoadProvidersAsync();
    }

    public ObservableCollection<AgentProviderOption> Providers => _providerOptions;

    public ObservableCollection<RoleOption> Roles => _roleOptions;

    public ObservableCollection<CapabilityOption> Capabilities => _capabilityOptions;

    public ICommand CreateAgentCommand => _createAgentCommand;

    public string PageTitle => "Create agent";

    public string PageSubtitle => "Choose a provider, role, model, and capabilities for a local agent profile.";

    public string AgentName
    {
        get => _agentName;
        set
        {
            if (!SetProperty(ref _agentName, value))
            {
                return;
            }

            _createAgentCommand.RaiseCanExecuteChanged();
        }
    }

    public string ModelName
    {
        get => _modelName;
        set
        {
            if (!SetProperty(ref _modelName, value))
            {
                return;
            }

            _createAgentCommand.RaiseCanExecuteChanged();
        }
    }

    public string SystemPrompt
    {
        get => _systemPrompt;
        set => SetProperty(ref _systemPrompt, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public AgentProviderOption? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (!SetProperty(ref _selectedProvider, value))
            {
                return;
            }

            if (value is not null)
            {
                ModelName = string.IsNullOrWhiteSpace(value.InstalledVersion)
                    ? ResolveDefaultModel(value.Kind)
                    : ModelName;

                StatusMessage = value.CanCreateAgents
                    ? $"Ready to create an agent with {value.DisplayName}."
                    : value.StatusSummary;
            }

            _createAgentCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task LoadProvidersAsync()
    {
        var workspace = await _agentSessionService.GetWorkspaceAsync(CancellationToken.None);
        _providerOptions.Clear();

        foreach (var provider in workspace.Providers)
        {
            _providerOptions.Add(
                new AgentProviderOption(
                    provider.Kind,
                    provider.DisplayName,
                    provider.CommandName,
                    provider.StatusSummary,
                    provider.InstalledVersion,
                    provider.CanCreateAgents));
        }

        SelectedProvider = _providerOptions.FirstOrDefault(option => option.CanCreateAgents) ??
            _providerOptions.FirstOrDefault();
    }

    private async Task CreateAgentAsync()
    {
        if (SelectedProvider is null)
        {
            StatusMessage = "Wait for provider readiness before creating an agent.";
            return;
        }

        if (!SelectedProvider.CanCreateAgents)
        {
            StatusMessage = SelectedProvider.StatusSummary;
            return;
        }

        if (string.IsNullOrWhiteSpace(AgentName) || string.IsNullOrWhiteSpace(ModelName))
        {
            StatusMessage = AgentValidationMessage;
            return;
        }

        StatusMessage = AgentCreationProgressMessage;

        try
        {
            var created = await _agentSessionService.CreateAgentAsync(
                new CreateAgentProfileCommand(
                    AgentName.Trim(),
                    ResolveSelectedRole(),
                    SelectedProvider.Kind,
                    ModelName.Trim(),
                    SystemPrompt.Trim(),
                    _capabilityOptions
                        .Where(option => option.IsEnabled)
                        .Select(option => option.Name)
                        .ToArray()),
                CancellationToken.None);

            StatusMessage = $"Created {created.Name} using {created.ProviderDisplayName}.";
            AgentName = $"{created.Name} Copy";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private AgentRoleKind ResolveSelectedRole()
    {
        return _roleOptions.FirstOrDefault(option => option.IsSelected)?.Role ?? AgentRoleKind.Operator;
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
