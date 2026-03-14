using DotPilot.Core.Features.AgentSessions;
using Microsoft.Extensions.Logging;

namespace DotPilot.Presentation;

internal static partial class MainViewModelLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Loading chat workspace snapshot.")]
    public static partial void LoadingWorkspace(ILogger logger);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Chat workspace snapshot loaded. Sessions={SessionCount} Agents={AgentCount}.")]
    public static partial void WorkspaceLoaded(ILogger logger, int sessionCount, int agentCount);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Refreshing chat workspace and provider state.")]
    public static partial void RefreshRequested(ILogger logger);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Starting new chat session from the chat shell.")]
    public static partial void StartingSession(ILogger logger);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Sending chat message from the shell. SessionId={SessionId} CharacterCount={CharacterCount}.")]
    public static partial void SendRequested(ILogger logger, string sessionId, int characterCount);

    [LoggerMessage(
        EventId = 2006,
        Level = LogLevel.Information,
        Message = "Chat shell send completed. SessionId={SessionId}.")]
    public static partial void SendCompleted(ILogger logger, string sessionId);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Error, Message = "Chat shell operation failed.")]
    public static partial void Failure(ILogger logger, Exception exception);
}

internal static partial class SecondViewModelLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Loading provider list for agent creation.")]
    public static partial void LoadingProviders(ILogger logger);

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Loaded provider list for agent creation. Providers={ProviderCount}.")]
    public static partial void ProvidersLoaded(ILogger logger, int providerCount);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Information,
        Message = "Creating local agent profile. Name={AgentName} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void AgentCreationRequested(
        ILogger logger,
        string agentName,
        AgentProviderKind providerKind,
        string modelName);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "Created local agent profile. AgentId={AgentId} Name={AgentName} Provider={ProviderKind} Model={ModelName}.")]
    public static partial void AgentCreated(
        ILogger logger,
        Guid agentId,
        string agentName,
        AgentProviderKind providerKind,
        string modelName);

    [LoggerMessage(EventId = 2104, Level = LogLevel.Error, Message = "Agent builder operation failed.")]
    public static partial void Failure(ILogger logger, Exception exception);
}

internal static partial class SettingsViewModelLog
{
    [LoggerMessage(EventId = 2200, Level = LogLevel.Information, Message = "Loading provider readiness settings.")]
    public static partial void LoadingProviders(ILogger logger);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Loaded provider readiness settings. Providers={ProviderCount}.")]
    public static partial void ProvidersLoaded(ILogger logger, int providerCount);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Refreshing provider readiness settings.")]
    public static partial void RefreshRequested(ILogger logger);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Error, Message = "Provider settings operation failed.")]
    public static partial void Failure(ILogger logger, Exception exception);
}

internal static partial class AppLog
{
    [LoggerMessage(EventId = 2300, Level = LogLevel.Information, Message = "{StartupMarker}")]
    public static partial void StartupMarker(ILogger logger, string startupMarker);
}
