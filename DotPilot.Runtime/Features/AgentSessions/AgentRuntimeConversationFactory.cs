using System.Globalization;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class AgentRuntimeConversationFactory(
    AgentSessionStorageOptions storageOptions,
    AgentExecutionLoggingMiddleware executionLoggingMiddleware,
    LocalAgentSessionStateStore sessionStateStore,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<AgentRuntimeConversationFactory> logger)
{
    private const string LiveExecutionUnavailableFormat = "Live desktop execution for {0} is not available in this build.";
    private static readonly System.Text.CompositeFormat LiveExecutionUnavailableCompositeFormat =
        System.Text.CompositeFormat.Parse(LiveExecutionUnavailableFormat);

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
        var agent = CreateAgent(agentRecord, descriptor, historyProvider);
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

    private AIAgent CreateAgent(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        FolderChatHistoryProvider historyProvider)
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

        AgentRuntimeConversationFactoryLog.AgentRuntimeCreated(
            logger,
            agentRecord.Id,
            agentRecord.Name,
            descriptor.ProviderKind);

        var agent = CreateChatClient(agentRecord, descriptor.ProviderDisplayName)
            .AsAIAgent(options, loggerFactory, serviceProvider);

        return executionLoggingMiddleware.AttachAgentRunLogging(agent, descriptor);
    }

    private static AgentExecutionDescriptor CreateExecutionDescriptor(AgentProfileRecord agentRecord)
    {
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)agentRecord.ProviderKind);
        return new AgentExecutionDescriptor(
            agentRecord.Id,
            agentRecord.Name,
            providerProfile.Kind,
            providerProfile.DisplayName,
            agentRecord.ModelName);
    }

    private IChatClient CreateChatClient(AgentProfileRecord agentRecord, string providerDisplayName)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var providerKind = (AgentProviderKind)agentRecord.ProviderKind;
        return providerKind switch
        {
            AgentProviderKind.Debug => new DebugChatClient(agentRecord.Name, timeProvider),
            AgentProviderKind.Codex => CreateCodexChatClient(agentRecord),
            _ => new UnavailableChatClient(
                string.Format(
                    CultureInfo.InvariantCulture,
                    LiveExecutionUnavailableCompositeFormat,
                    providerDisplayName)),
        };
    }

    private CodexChatClient CreateCodexChatClient(AgentProfileRecord agentRecord)
    {
        var providerProfile = AgentSessionProviderCatalog.Get(AgentProviderKind.Codex);
        return new CodexChatClient(
            agentRecord.Name,
            agentRecord.ModelName,
            DeserializeCapabilities(agentRecord.CapabilitiesJson),
            AgentSessionCommandProbe.ResolveExecutablePath(providerProfile.CommandName),
            Environment.CurrentDirectory,
            timeProvider,
            serviceProvider.GetRequiredService<ILogger<CodexChatClient>>());
    }

    private static string[] DeserializeCapabilities(string capabilitiesJson)
    {
        return System.Text.Json.JsonSerializer.Deserialize(
            capabilitiesJson,
            AgentSessionJsonSerializerContext.Default.StringArray) ?? [];
    }
}

internal sealed record RuntimeConversationContext(
    AIAgent Agent,
    AgentSession Session,
    AgentExecutionDescriptor Descriptor,
    bool IsTransient = false);
