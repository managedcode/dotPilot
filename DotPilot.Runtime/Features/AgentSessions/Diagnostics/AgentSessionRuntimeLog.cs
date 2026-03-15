using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal static partial class AgentProviderStatusCacheLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Using cached provider readiness snapshot. AgeMilliseconds={AgeMilliseconds}.")]
    public static partial void CacheHit(ILogger logger, double ageMilliseconds);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Refreshing provider readiness snapshot. ForceRefresh={ForceRefresh}.")]
    public static partial void RefreshStarted(ILogger logger, bool forceRefresh);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Provider readiness snapshot refreshed for {ProviderCount} providers in {ElapsedMilliseconds} ms.")]
    public static partial void RefreshCompleted(ILogger logger, int providerCount, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Provider probe completed. Provider={ProviderKind} Status={Status} Enabled={IsEnabled} CanCreateAgents={CanCreateAgents} InstalledVersion={InstalledVersion} ExecutablePath={ExecutablePath}.")]
    public static partial void ProbeCompleted(
        ILogger logger,
        AgentProviderKind providerKind,
        AgentProviderStatus status,
        bool isEnabled,
        bool canCreateAgents,
        string installedVersion,
        string executablePath);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Error,
        Message = "Provider readiness snapshot refresh failed.")]
    public static partial void RefreshFailed(ILogger logger, Exception exception);
}

internal static partial class AgentRuntimeConversationFactoryLog
{
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Initializing runtime conversation state. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void InitializeStarted(ILogger logger, SessionId sessionId, Guid agentId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Loaded persisted runtime conversation state. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void SessionLoaded(ILogger logger, SessionId sessionId, Guid agentId);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "Created new runtime conversation session. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void SessionCreated(ILogger logger, SessionId sessionId, Guid agentId);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Persisted runtime conversation state. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void SessionSaved(ILogger logger, SessionId sessionId, string agentId);

    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Information,
        Message = "Created runtime chat agent. AgentId={AgentId} Name={AgentName} Provider={ProviderKind}.")]
    public static partial void AgentRuntimeCreated(
        ILogger logger,
        Guid agentId,
        string agentName,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1105,
        Level = LogLevel.Information,
        Message = "Using transient runtime conversation path. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void TransientRuntimeConversation(ILogger logger, SessionId sessionId, Guid agentId);
}

internal static partial class AgentSessionServiceLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Initializing local agent session store.")]
    public static partial void InitializationStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Local agent session store initialized.")]
    public static partial void InitializationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 1217,
        Level = LogLevel.Information,
        Message = "Enabled default provider preference. Provider={ProviderKind}.")]
    public static partial void DefaultProviderEnabled(ILogger logger, AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1218,
        Level = LogLevel.Information,
        Message = "Seeded default system agent. AgentId={AgentId} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void DefaultAgentSeeded(
        ILogger logger,
        Guid agentId,
        AgentProviderKind providerKind,
        string modelName);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Information,
        Message = "Loaded workspace snapshot. Sessions={SessionCount} Agents={AgentCount} Providers={ProviderCount}.")]
    public static partial void WorkspaceLoaded(ILogger logger, int sessionCount, int agentCount, int providerCount);

    [LoggerMessage(
        EventId = 1203,
        Level = LogLevel.Information,
        Message = "Loaded session transcript. SessionId={SessionId} EntryCount={EntryCount} ParticipantCount={ParticipantCount}.")]
    public static partial void SessionLoaded(ILogger logger, SessionId sessionId, int entryCount, int participantCount);

    [LoggerMessage(
        EventId = 1204,
        Level = LogLevel.Information,
        Message = "Session transcript was requested but not found. SessionId={SessionId}.")]
    public static partial void SessionNotFound(ILogger logger, SessionId sessionId);

    [LoggerMessage(
        EventId = 1205,
        Level = LogLevel.Information,
        Message = "Creating agent profile. Name={AgentName} Provider={ProviderKind} Role={Role}.")]
    public static partial void AgentCreationStarted(
        ILogger logger,
        string agentName,
        AgentProviderKind providerKind,
        AgentRoleKind role);

    [LoggerMessage(
        EventId = 1206,
        Level = LogLevel.Information,
        Message = "Created agent profile. AgentId={AgentId} Name={AgentName} Provider={ProviderKind}.")]
    public static partial void AgentCreated(
        ILogger logger,
        Guid agentId,
        string agentName,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1207,
        Level = LogLevel.Error,
        Message = "Agent profile creation failed. Name={AgentName} Provider={ProviderKind}.")]
    public static partial void AgentCreationFailed(
        ILogger logger,
        Exception exception,
        string agentName,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1208,
        Level = LogLevel.Information,
        Message = "Creating session. Title={SessionTitle} AgentId={AgentId}.")]
    public static partial void SessionCreationStarted(ILogger logger, string sessionTitle, AgentProfileId agentId);

    [LoggerMessage(
        EventId = 1209,
        Level = LogLevel.Information,
        Message = "Created session. SessionId={SessionId} Title={SessionTitle} AgentId={AgentId}.")]
    public static partial void SessionCreated(
        ILogger logger,
        SessionId sessionId,
        string sessionTitle,
        AgentProfileId agentId);

    [LoggerMessage(
        EventId = 1210,
        Level = LogLevel.Information,
        Message = "Updated provider preference. Provider={ProviderKind} IsEnabled={IsEnabled}.")]
    public static partial void ProviderPreferenceUpdated(
        ILogger logger,
        AgentProviderKind providerKind,
        bool isEnabled);

    [LoggerMessage(
        EventId = 1211,
        Level = LogLevel.Error,
        Message = "Provider preference update failed. Provider={ProviderKind} IsEnabled={IsEnabled}.")]
    public static partial void ProviderPreferenceUpdateFailed(
        ILogger logger,
        Exception exception,
        AgentProviderKind providerKind,
        bool isEnabled);

    [LoggerMessage(
        EventId = 1219,
        Level = LogLevel.Information,
        Message = "Updated composer send behavior preference. Behavior={Behavior}.")]
    public static partial void ComposerSendBehaviorUpdated(
        ILogger logger,
        ComposerSendBehavior behavior);

    [LoggerMessage(
        EventId = 1212,
        Level = LogLevel.Information,
        Message = "Starting session send. SessionId={SessionId} AgentId={AgentId} Provider={ProviderKind}.")]
    public static partial void SendStarted(
        ILogger logger,
        SessionId sessionId,
        Guid agentId,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1213,
        Level = LogLevel.Warning,
        Message = "Session send blocked because provider is disabled. SessionId={SessionId} Provider={ProviderKind}.")]
    public static partial void SendBlockedDisabled(
        ILogger logger,
        SessionId sessionId,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1214,
        Level = LogLevel.Warning,
        Message = "Session send blocked because provider runtime is not wired. SessionId={SessionId} Provider={ProviderKind}.")]
    public static partial void SendBlockedNotWired(
        ILogger logger,
        SessionId sessionId,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1215,
        Level = LogLevel.Information,
        Message = "Completed session send. SessionId={SessionId} AgentId={AgentId} AssistantCharacters={AssistantCharacterCount}.")]
    public static partial void SendCompleted(
        ILogger logger,
        SessionId sessionId,
        Guid agentId,
        int assistantCharacterCount);

    [LoggerMessage(
        EventId = 1216,
        Level = LogLevel.Error,
        Message = "Session send failed. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void SendFailed(ILogger logger, Exception exception, SessionId sessionId, Guid agentId);
}

internal static partial class AgentWorkspaceStateLog
{
    [LoggerMessage(
        EventId = 1300,
        Level = LogLevel.Information,
        Message = "Returning cached workspace projection. Sessions={SessionCount} Agents={AgentCount} Providers={ProviderCount}.")]
    public static partial void WorkspaceCacheHit(ILogger logger, int sessionCount, int agentCount, int providerCount);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Workspace projection refreshed. Sessions={SessionCount} Agents={AgentCount} Providers={ProviderCount}.")]
    public static partial void WorkspaceRefreshed(ILogger logger, int sessionCount, int agentCount, int providerCount);

    [LoggerMessage(
        EventId = 1302,
        Level = LogLevel.Information,
        Message = "Returning cached session projection. SessionId={SessionId} Entries={EntryCount}.")]
    public static partial void SessionCacheHit(ILogger logger, SessionId sessionId, int entryCount);

    [LoggerMessage(
        EventId = 1303,
        Level = LogLevel.Information,
        Message = "Cached workspace updated after agent creation. AgentId={AgentId} Provider={ProviderKind}.")]
    public static partial void AgentCached(ILogger logger, AgentProfileId agentId, AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1304,
        Level = LogLevel.Information,
        Message = "Cached workspace updated after session creation. SessionId={SessionId} AgentId={AgentId}.")]
    public static partial void SessionCached(ILogger logger, SessionId sessionId, AgentProfileId agentId);

    [LoggerMessage(
        EventId = 1305,
        Level = LogLevel.Information,
        Message = "Cached workspace updated after provider preference change. Provider={ProviderKind} Enabled={IsEnabled}.")]
    public static partial void ProviderCached(ILogger logger, AgentProviderKind providerKind, bool isEnabled);
}
