using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.AgentSessions;

public sealed record CreateAgentProfileCommand(
    string Name,
    AgentRoleKind Role,
    AgentProviderKind ProviderKind,
    string ModelName,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities);

public sealed record CreateSessionCommand(
    string Title,
    AgentProfileId AgentProfileId);

public sealed record SendSessionMessageCommand(
    SessionId SessionId,
    string Message);

public sealed record UpdateProviderPreferenceCommand(
    AgentProviderKind ProviderKind,
    bool IsEnabled);

