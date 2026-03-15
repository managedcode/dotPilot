using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed partial record AgentProviderOption(
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    string StatusSummary,
    string? InstalledVersion,
    bool CanCreateAgents);

[Bindable]
public sealed class CapabilityOption(
    string name,
    string description,
    bool isEnabled) : ObservableObject
{
    private bool _isEnabled = isEnabled;

    public string Name { get; } = name;

    public string Description { get; } = description;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

[Bindable]
public sealed partial record RoleOption(
    string Label,
    AgentRoleKind Role);

[Bindable]
public sealed partial record AgentCatalogItem(
    AgentProfileId Id,
    string Initial,
    string Name,
    string Description,
    string ProviderDisplayName,
    string ModelName,
    IReadOnlyList<string> Tags,
    bool IsDefault);

[Bindable]
public sealed partial record AgentBuilderSurface(
    string Title,
    string Subtitle,
    bool ShowCatalog,
    bool ShowPromptComposer,
    bool ShowEditor,
    bool ShowBackButton,
    string PrimaryActionLabel);
