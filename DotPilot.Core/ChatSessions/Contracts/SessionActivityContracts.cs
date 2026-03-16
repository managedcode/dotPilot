using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.ChatSessions.Contracts;

public sealed record SessionActivityDescriptor(
    SessionId SessionId,
    string SessionTitle,
    AgentProfileId AgentProfileId,
    string AgentName,
    string ProviderDisplayName);

public sealed record SessionActivitySnapshot(
    bool HasActiveSessions,
    int ActiveSessionCount,
    IReadOnlyList<SessionActivityDescriptor> ActiveSessions,
    SessionId? SessionId,
    string SessionTitle,
    AgentProfileId? AgentProfileId,
    string AgentName,
    string ProviderDisplayName);
