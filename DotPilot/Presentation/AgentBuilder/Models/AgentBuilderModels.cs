using DotPilot.Core.ControlPlaneDomain;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

public enum AgentBuilderSurfaceKind
{
    Catalog,
    PromptComposer,
    Editor,
}

[Bindable]
public sealed partial record AgentProviderOption(
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    string StatusSummary,
    string? InstalledVersion,
    bool CanCreateAgents);

[Bindable]
public sealed class SelectionOption(
    string key,
    string label,
    string description,
    bool isEnabled) : ObservableObject
{
    private bool _isEnabled = isEnabled;

    public string Key { get; } = key;

    public string Label { get; } = label;

    public string Description { get; } = description;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

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
    AgentBuilderSurfaceKind Kind,
    string Title,
    string Subtitle,
    bool ShowBackButton,
    string PrimaryActionLabel)
{
    public bool ShowCatalog => Kind == AgentBuilderSurfaceKind.Catalog;

    public bool ShowPromptComposer => Kind == AgentBuilderSurfaceKind.PromptComposer;

    public bool ShowEditor => Kind == AgentBuilderSurfaceKind.Editor;
}
