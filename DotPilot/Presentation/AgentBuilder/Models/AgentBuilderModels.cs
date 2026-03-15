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
    IReadOnlyList<string> SupportedModelNames,
    string? InstalledVersion,
    bool CanCreateAgents)
{
    public string SelectionAutomationId => AgentBuilderAutomationIds.ForProvider(Kind);
}

[Bindable]
public sealed partial record AgentModelOption(
    string DisplayName,
    string SelectionAutomationId);

[Bindable]
public sealed partial record AgentCatalogStartChatRequest(
    AgentProfileId AgentId,
    string AgentName);

[Bindable]
public sealed partial record AgentCatalogItem(
    AgentProfileId Id,
    string Initial,
    string Name,
    string Description,
    string ProviderDisplayName,
    string ModelName,
    bool IsDefault,
    string StartChatAutomationId,
    AgentCatalogStartChatRequest StartChatRequest,
    ICommand? StartChatCommand);

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

internal static class AgentBuilderAutomationIds
{
    public static string ForProvider(AgentProviderKind kind)
    {
        return "AgentProviderOption_" + CreateAutomationIdSuffix(kind.ToString());
    }

    public static string ForModel(string modelName)
    {
        return "AgentModelOption_" + CreateAutomationIdSuffix(modelName);
    }

    private static string CreateAutomationIdSuffix(string value)
    {
        var characters = value.Where(char.IsLetterOrDigit).ToArray();
        return characters.Length == 0 ? "Unknown" : new string(characters);
    }
}
