using DotPilot.Core;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed partial record FleetBoardView(
    IReadOnlyList<FleetBoardMetricItem> Metrics,
    IReadOnlyList<FleetBoardSessionItem> ActiveSessions,
    IReadOnlyList<FleetBoardProviderItem> Providers,
    bool HasActiveSessions,
    bool ShowActiveSessionsEmptyState,
    string ActiveSessionsEmptyMessage);

[Bindable]
public sealed partial record FleetBoardMetricItem(
    string Label,
    string Value,
    string Summary,
    string AutomationId);

[Bindable]
public sealed partial record FleetBoardSessionRequest(
    SessionId SessionId,
    string SessionTitle);

[Bindable]
public sealed partial record FleetBoardSessionItem(
    string Title,
    string Summary,
    string AutomationId,
    FleetBoardSessionRequest OpenRequest,
    ICommand? OpenCommand);

[Bindable]
public sealed partial record FleetBoardProviderItem(
    string DisplayName,
    string StatusLabel,
    string Summary,
    Brush? StatusBrush,
    string AutomationId);

internal static class FleetBoardAutomationIds
{
    public static string ForMetric(string label)
    {
        return "ChatFleetMetric_" + CreateAutomationIdSuffix(label);
    }

    public static string ForSession(string sessionTitle)
    {
        return "ChatFleetSession_" + CreateAutomationIdSuffix(sessionTitle);
    }

    public static string ForProvider(string providerName)
    {
        return "ChatFleetProvider_" + CreateAutomationIdSuffix(providerName);
    }

    private static string CreateAutomationIdSuffix(string value)
    {
        var characters = value.Where(char.IsLetterOrDigit).ToArray();
        return characters.Length == 0 ? "Unknown" : new string(characters);
    }
}
