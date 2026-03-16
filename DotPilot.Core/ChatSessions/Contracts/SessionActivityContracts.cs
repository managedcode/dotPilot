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
    SessionId? SessionId,
    string SessionTitle,
    AgentProfileId? AgentProfileId,
    string AgentName,
    string ProviderDisplayName);
