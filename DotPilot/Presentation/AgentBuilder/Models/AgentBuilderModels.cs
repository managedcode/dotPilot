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
    string SuggestedModelName,
    string? InstalledVersion,
    bool CanCreateAgents);

[Bindable]
public sealed partial record AgentCatalogItem(
    AgentProfileId Id,
    string Initial,
    string Name,
    string Description,
    string ProviderDisplayName,
    string ModelName,
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
