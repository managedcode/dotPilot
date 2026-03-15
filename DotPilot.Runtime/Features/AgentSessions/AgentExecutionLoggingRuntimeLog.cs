using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal static partial class AgentRuntimeConversationFactoryLog
{
    [LoggerMessage(
        EventId = 1106,
        Level = LogLevel.Information,
        Message = "Configured agent run middleware. AgentId={AgentId} AgentName={AgentName} Provider={ProviderKind}.")]
    public static partial void AgentMiddlewareConfigured(
        ILogger logger,
        Guid agentId,
        string agentName,
        AgentProviderKind providerKind);

    [LoggerMessage(
        EventId = 1107,
        Level = LogLevel.Information,
        Message = "Configured run-scoped chat logging. RunId={RunId} SessionId={SessionId} AgentId={AgentId} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void RunScopedChatLoggingConfigured(
        ILogger logger,
        string runId,
        SessionId sessionId,
        Guid agentId,
        AgentProviderKind providerKind,
        string modelName);

    [LoggerMessage(
        EventId = 1108,
        Level = LogLevel.Information,
        Message = "Agent run started. RunId={RunId} SessionId={SessionId} AgentId={AgentId} AgentName={AgentName} Provider={ProviderKind} IsStreaming={IsStreaming} MessageCount={MessageCount}.")]
    public static partial void AgentRunStarted(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        string agentName,
        AgentProviderKind providerKind,
        bool isStreaming,
        int messageCount);

    [LoggerMessage(
        EventId = 1109,
        Level = LogLevel.Information,
        Message = "Agent run completed. RunId={RunId} SessionId={SessionId} AgentId={AgentId} IsStreaming={IsStreaming} OutputCount={OutputCount} CharacterCount={CharacterCount} ElapsedMilliseconds={ElapsedMilliseconds}.")]
    public static partial void AgentRunCompleted(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        bool isStreaming,
        int outputCount,
        int characterCount,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1110,
        Level = LogLevel.Error,
        Message = "Agent run failed. RunId={RunId} SessionId={SessionId} AgentId={AgentId} IsStreaming={IsStreaming}.")]
    public static partial void AgentRunFailed(
        ILogger logger,
        Exception exception,
        string runId,
        string sessionId,
        Guid agentId,
        bool isStreaming);

    [LoggerMessage(
        EventId = 1111,
        Level = LogLevel.Information,
        Message = "Observed first agent streaming update. RunId={RunId} SessionId={SessionId} AgentId={AgentId} MessageId={MessageId} CharacterCount={CharacterCount}.")]
    public static partial void AgentRunFirstUpdateObserved(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        string messageId,
        int characterCount);

    [LoggerMessage(
        EventId = 1112,
        Level = LogLevel.Information,
        Message = "Chat client request started. RunId={RunId} SessionId={SessionId} AgentId={AgentId} IsStreaming={IsStreaming} Model={ModelName} MessageCount={MessageCount} ToolCount={ToolCount}.")]
    public static partial void ChatClientRequestStarted(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        bool isStreaming,
        string modelName,
        int messageCount,
        int toolCount);

    [LoggerMessage(
        EventId = 1113,
        Level = LogLevel.Information,
        Message = "Chat client request completed. RunId={RunId} SessionId={SessionId} AgentId={AgentId} IsStreaming={IsStreaming} OutputCount={OutputCount} CharacterCount={CharacterCount} ElapsedMilliseconds={ElapsedMilliseconds}.")]
    public static partial void ChatClientRequestCompleted(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        bool isStreaming,
        int outputCount,
        int characterCount,
        double elapsedMilliseconds);

    [LoggerMessage(
        EventId = 1114,
        Level = LogLevel.Error,
        Message = "Chat client request failed. RunId={RunId} SessionId={SessionId} AgentId={AgentId} IsStreaming={IsStreaming}.")]
    public static partial void ChatClientRequestFailed(
        ILogger logger,
        Exception exception,
        string runId,
        string sessionId,
        Guid agentId,
        bool isStreaming);

    [LoggerMessage(
        EventId = 1115,
        Level = LogLevel.Information,
        Message = "Observed first chat client streaming update. RunId={RunId} SessionId={SessionId} AgentId={AgentId} MessageId={MessageId} CharacterCount={CharacterCount}.")]
    public static partial void ChatClientFirstUpdateObserved(
        ILogger logger,
        string runId,
        string sessionId,
        Guid agentId,
        string messageId,
        int characterCount);
}

internal static partial class AgentSessionServiceLog
{
    [LoggerMessage(
        EventId = 1219,
        Level = LogLevel.Information,
        Message = "Prepared correlated agent run. SessionId={SessionId} AgentId={AgentId} RunId={RunId} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void SendRunPrepared(
        ILogger logger,
        SessionId sessionId,
        Guid agentId,
        string runId,
        AgentProviderKind providerKind,
        string modelName);
}
