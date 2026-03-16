using System.Globalization;
using DotPilot.Core.Providers;
using GitHub.Copilot.SDK;
using ManagedCode.ClaudeCodeSharpSDK.Configuration;
using ManagedCode.ClaudeCodeSharpSDK.Extensions.AI;
using ManagedCode.CodexSharpSDK.Configuration;
using ManagedCode.CodexSharpSDK.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClaudeThreadOptions = ManagedCode.ClaudeCodeSharpSDK.Client.ThreadOptions;
using CodexThreadOptions = ManagedCode.CodexSharpSDK.Client.ThreadOptions;

namespace DotPilot.Core.ChatSessions;

internal sealed class AgentRuntimeConversationFactory(
    AgentSessionStorageOptions storageOptions,
    AgentExecutionLoggingMiddleware executionLoggingMiddleware,
    LocalAgentSessionStateStore sessionStateStore,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<AgentRuntimeConversationFactory> logger)
{
    public async ValueTask InitializeAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        AgentRuntimeConversationFactoryLog.InitializeStarted(logger, sessionId, agentRecord.Id);
        if (ShouldUseTransientRuntimeConversation(agentRecord))
        {
            AgentRuntimeConversationFactoryLog.TransientRuntimeConversation(logger, sessionId, agentRecord.Id);
            return;
        }

        var runtimeSession = await LoadOrCreateAsync(agentRecord, sessionId, cancellationToken);
        await sessionStateStore.SaveAsync(runtimeSession.Agent, runtimeSession.Session, sessionId, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var agentRuntimeId = agentRecord.Id.ToString("N", CultureInfo.InvariantCulture);
            AgentRuntimeConversationFactoryLog.SessionSaved(
                logger,
                sessionId,
                agentRuntimeId);
        }
    }

    public async ValueTask<RuntimeConversationContext> LoadOrCreateAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var useTransientConversation = ShouldUseTransientRuntimeConversation(agentRecord);
        var historyProvider = new FolderChatHistoryProvider(
            serviceProvider.GetRequiredService<LocalAgentChatHistoryStore>());
        var descriptor = CreateExecutionDescriptor(agentRecord);
        var agent = await CreateAgentAsync(agentRecord, descriptor, historyProvider, sessionId, cancellationToken);
        if (useTransientConversation)
        {
            var transientSession = await CreateNewSessionAsync(agent, sessionId, cancellationToken);
            AgentRuntimeConversationFactoryLog.TransientRuntimeConversation(logger, sessionId, agentRecord.Id);
            return new RuntimeConversationContext(agent, transientSession, descriptor, IsTransient: true);
        }

        var session = await sessionStateStore.TryLoadAsync(agent, sessionId, cancellationToken);
        if (session is null)
        {
            session = await CreateNewSessionAsync(agent, sessionId, cancellationToken);
            await sessionStateStore.SaveAsync(agent, session, sessionId, cancellationToken);
            AgentRuntimeConversationFactoryLog.SessionCreated(logger, sessionId, agentRecord.Id);
        }
        else
        {
            AgentRuntimeConversationFactoryLog.SessionLoaded(logger, sessionId, agentRecord.Id);
        }

        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return new RuntimeConversationContext(agent, session, descriptor);
    }

    public ValueTask SaveAsync(
        RuntimeConversationContext runtimeContext,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        if (runtimeContext.IsTransient)
        {
            return ValueTask.CompletedTask;
        }

        AgentRuntimeConversationFactoryLog.SessionSaved(logger, sessionId, runtimeContext.Agent.Id);
        return sessionStateStore.SaveAsync(runtimeContext.Agent, runtimeContext.Session, sessionId, cancellationToken);
    }

    private bool ShouldUseTransientRuntimeConversation(AgentProfileRecord agentRecord)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var providerKind = (AgentProviderKind)agentRecord.ProviderKind;
        return storageOptions.PreferTransientRuntimeConversation ||
            (OperatingSystem.IsBrowser() && providerKind == AgentProviderKind.Debug);
    }

    private static async ValueTask<AgentSession> CreateNewSessionAsync(
        AIAgent agent,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);
        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return session;
    }

    private async ValueTask<AIAgent> CreateAgentAsync(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        FolderChatHistoryProvider historyProvider,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        AgentRuntimeConversationFactoryLog.AgentRuntimeCreated(
            logger,
            agentRecord.Id,
            agentRecord.Name,
            descriptor.ProviderKind);

        var agent = descriptor.ProviderKind switch
        {
            AgentProviderKind.GitHubCopilot => await CreateGitHubCopilotAgentAsync(
                agentRecord,
                descriptor,
                sessionId,
                cancellationToken),
            _ => CreateChatClientAgent(
                agentRecord,
                descriptor,
                historyProvider,
                CreateChatClient(descriptor.ProviderKind, agentRecord.Name, sessionId, agentRecord.ModelName)),
        };

        return executionLoggingMiddleware.AttachAgentRunLogging(agent, descriptor);
    }

    private static AgentExecutionDescriptor CreateExecutionDescriptor(AgentProfileRecord agentRecord)
    {
        var providerKind = (AgentProviderKind)agentRecord.ProviderKind;
        return new AgentExecutionDescriptor(
            agentRecord.Id,
            agentRecord.Name,
            providerKind,
            providerKind.GetDisplayName(),
            agentRecord.ModelName);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The runtime conversation factory intentionally preserves the IChatClient abstraction across provider-backed chat clients.")]
    private IChatClient CreateChatClient(
        AgentProviderKind providerKind,
        string agentName,
        SessionId sessionId,
        string modelName)
    {
        if (providerKind == AgentProviderKind.Debug)
        {
            return new DebugChatClient(agentName, timeProvider);
        }

        if (providerKind == AgentProviderKind.Codex)
        {
            return new CodexChatClient(new CodexChatClientOptions
            {
                CodexOptions = new CodexOptions(),
                DefaultModel = modelName,
                DefaultThreadOptions = new CodexThreadOptions
                {
                    Model = modelName,
                    WorkingDirectory = ResolvePlaygroundDirectory(sessionId),
                },
            });
        }

        if (providerKind == AgentProviderKind.ClaudeCode)
        {
            return new ClaudeChatClient(new ClaudeChatClientOptions
            {
                ClaudeOptions = new ClaudeOptions(),
                DefaultModel = modelName,
                DefaultThreadOptions = new ClaudeThreadOptions
                {
                    Model = modelName,
                    WorkingDirectory = ResolvePlaygroundDirectory(sessionId),
                },
            });
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} live execution is unavailable.",
                providerKind.GetDisplayName()));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The factory returns the concrete ChatClientAgent only for the chat-client-backed providers and keeps the outer flow on AIAgent.")]
    private ChatClientAgent CreateChatClientAgent(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        FolderChatHistoryProvider historyProvider,
        IChatClient chatClient)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var options = new ChatClientAgentOptions
        {
            Id = agentRecord.Id.ToString("N", CultureInfo.InvariantCulture),
            Name = agentRecord.Name,
            Description = descriptor.ProviderDisplayName,
            ChatHistoryProvider = historyProvider,
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = agentRecord.SystemPrompt,
                ModelId = agentRecord.ModelName,
            },
        };

        return (ChatClientAgent)chatClient.AsAIAgent(options, loggerFactory, serviceProvider);
    }

    private async ValueTask<AIAgent> CreateGitHubCopilotAgentAsync(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var workingDirectory = ResolvePlaygroundDirectory(sessionId);
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            AutoStart = false,
            UseStdio = true,
        });

        await copilotClient.StartAsync(cancellationToken);

        return copilotClient.AsAIAgent(
            new SessionConfig
            {
                Model = agentRecord.ModelName,
                SystemMessage = new SystemMessageConfig
                {
                    Content = agentRecord.SystemPrompt,
                },
                WorkingDirectory = workingDirectory,
            },
            ownsClient: true,
            id: agentRecord.Id.ToString("N", CultureInfo.InvariantCulture),
            name: agentRecord.Name,
            description: descriptor.ProviderDisplayName);
    }

    private string ResolvePlaygroundDirectory(SessionId sessionId)
    {
        var directory = AgentSessionStoragePaths.ResolvePlaygroundDirectory(storageOptions, sessionId);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
