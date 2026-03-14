using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Presentation;

public sealed partial record AgentProviderOption(
    AgentProviderKind Kind,
    string DisplayName,
    string CommandName,
    string StatusSummary,
    string? InstalledVersion,
    bool CanCreateAgents);

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

public sealed class RoleOption(
    string label,
    AgentRoleKind role,
    bool isSelected) : ObservableObject
{
    private bool _isSelected = isSelected;

    public string Label { get; } = label;

    public AgentRoleKind Role { get; } = role;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
