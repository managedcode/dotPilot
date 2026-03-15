using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal static partial class AgentProviderStatusReaderLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Reading provider readiness state from local sources.")]
    public static partial void ReadStarted(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Provider readiness state read for {ProviderCount} providers in {ElapsedMilliseconds} ms.")]
    public static partial void ReadCompleted(ILogger logger, int providerCount, double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1002,
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
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Provider readiness state read failed.")]
    public static partial void ReadFailed(ILogger logger, Exception exception);
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
        EventId = 1220,
        Level = LogLevel.Error,
        Message = "Workspace snapshot load failed.")]
    public static partial void WorkspaceLoadFailed(ILogger logger, Exception exception);

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
        EventId = 1221,
        Level = LogLevel.Error,
        Message = "Session transcript load failed. SessionId={SessionId}.")]
    public static partial void SessionLoadFailed(ILogger logger, Exception exception, SessionId sessionId);

    [LoggerMessage(
        EventId = 1205,
        Level = LogLevel.Information,
        Message = "Creating agent profile. Name={AgentName} Provider={ProviderKind}.")]
    public static partial void AgentCreationStarted(
        ILogger logger,
        string agentName,
        AgentProviderKind providerKind);

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
        EventId = 1222,
        Level = LogLevel.Error,
        Message = "Session creation failed. Title={SessionTitle} AgentId={AgentId}.")]
    public static partial void SessionCreationFailed(
        ILogger logger,
        Exception exception,
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
