using System.Globalization;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class AgentRuntimeConversationFactory(
    LocalAgentSessionStateStore sessionStateStore,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider)
{
    private const string NotYetImplementedFormat = "{0} live CLI execution is not wired yet in this slice.";
    private static readonly System.Text.CompositeFormat NotYetImplementedCompositeFormat =
        System.Text.CompositeFormat.Parse(NotYetImplementedFormat);

    public async ValueTask InitializeAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var runtimeSession = await LoadOrCreateAsync(agentRecord, sessionId, cancellationToken);
        await sessionStateStore.SaveAsync(runtimeSession.Agent, runtimeSession.Session, sessionId, cancellationToken);
    }

    public async ValueTask<RuntimeConversationContext> LoadOrCreateAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var historyProvider = new FolderChatHistoryProvider(
            serviceProvider.GetRequiredService<LocalAgentChatHistoryStore>());
        var agent = CreateAgent(agentRecord, historyProvider);
        var session = await sessionStateStore.TryLoadAsync(agent, sessionId, cancellationToken);
        if (session is null)
        {
            session = await CreateNewSessionAsync(agent, sessionId, cancellationToken);
            await sessionStateStore.SaveAsync(agent, session, sessionId, cancellationToken);
        }

        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return new RuntimeConversationContext(agent, session);
    }

    public ValueTask SaveAsync(
        RuntimeConversationContext runtimeContext,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        return sessionStateStore.SaveAsync(runtimeContext.Agent, runtimeContext.Session, sessionId, cancellationToken);
    }

    private static async ValueTask<AgentSession> CreateNewSessionAsync(
        ChatClientAgent agent,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);
        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return session;
    }

    private ChatClientAgent CreateAgent(
        AgentProfileRecord agentRecord,
        FolderChatHistoryProvider historyProvider)
    {
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)agentRecord.ProviderKind);
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var options = new ChatClientAgentOptions
        {
            Id = agentRecord.Id.ToString("N", CultureInfo.InvariantCulture),
            Name = agentRecord.Name,
            Description = providerProfile.DisplayName,
            ChatHistoryProvider = historyProvider,
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = agentRecord.SystemPrompt,
                ModelId = agentRecord.ModelName,
            },
        };

        return CreateChatClient(providerProfile, agentRecord.Name)
            .AsAIAgent(options, loggerFactory, serviceProvider);
    }

    private IChatClient CreateChatClient(
        AgentSessionProviderProfile providerProfile,
        string agentName)
    {
        return providerProfile.Kind == AgentProviderKind.Debug
            ? new DebugChatClient(agentName, timeProvider)
            : new UnavailableChatClient(
                string.Format(
                    CultureInfo.InvariantCulture,
                    NotYetImplementedCompositeFormat,
                    providerProfile.DisplayName));
    }
}

internal sealed record RuntimeConversationContext(ChatClientAgent Agent, AgentSession Session);
