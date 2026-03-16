using System.Diagnostics;
using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed partial class AgentExecutionLoggingMiddleware(
    ILogger<AgentExecutionLoggingMiddleware> logger)
{
    public AIAgent AttachAgentRunLogging(
        AIAgent agent,
        AgentExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(descriptor);

        AgentRuntimeConversationFactoryLog.AgentMiddlewareConfigured(
            logger,
            descriptor.AgentId,
            descriptor.AgentName,
            descriptor.ProviderKind);

        return agent
            .AsBuilder()
            .Use(
                runFunc: (messages, session, options, innerAgent, cancellationToken) =>
                    LogAgentRunAsync(
                        descriptor,
                        messages,
                        session,
                        options,
                        innerAgent,
                        cancellationToken),
                runStreamingFunc: (messages, session, options, innerAgent, cancellationToken) =>
                    LogAgentRunStreamingAsync(
                        descriptor,
                        messages,
                        session,
                        options,
                        innerAgent,
                        cancellationToken))
            .Build();
    }

    public AgentExecutionRunConfiguration CreateRunConfiguration(
        AgentExecutionDescriptor descriptor,
        SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var runContext = new AgentRunLogContext(
            Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture),
            sessionId.Value.ToString("N", CultureInfo.InvariantCulture),
            descriptor.AgentId,
            descriptor.AgentName,
            descriptor.ProviderKind,
            descriptor.ProviderDisplayName,
            descriptor.ModelName);

        var options = new ChatClientAgentRunOptions(chatOptions: null)
        {
            AdditionalProperties = CreateAdditionalProperties(runContext),
            ChatClientFactory = chatClient => WrapChatClient(chatClient, runContext),
        };

        AgentRuntimeConversationFactoryLog.RunScopedChatLoggingConfigured(
            logger,
            runContext.RunId,
            sessionId,
            descriptor.AgentId,
            descriptor.ProviderKind,
            descriptor.ModelName);

        return new AgentExecutionRunConfiguration(runContext, options);
    }

    private async Task<AgentResponse> LogAgentRunAsync(
        AgentExecutionDescriptor descriptor,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        var runContext = ResolveRunContext(descriptor, options);
        using var scope = BeginScope(runContext);
        var stopwatch = Stopwatch.StartNew();
        var isInfoEnabled = logger.IsEnabled(LogLevel.Information);

        if (isInfoEnabled)
        {
            AgentRuntimeConversationFactoryLog.AgentRunStarted(
                logger,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                runContext.AgentName,
                runContext.ProviderKind,
                false,
                materializedMessages.Count);
        }

        try
        {
            var response = await innerAgent.RunAsync(
                materializedMessages,
                session,
                options,
                cancellationToken);

            if (isInfoEnabled)
            {
                var outputCount = response.Messages.Count;
                var characterCount = CountMessageCharacters(response.Messages);
                AgentRuntimeConversationFactoryLog.AgentRunCompleted(
                    logger,
                    runContext.RunId,
                    runContext.SessionId,
                    runContext.AgentId,
                    false,
                    outputCount,
                    characterCount,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            return response;
        }
        catch (Exception exception)
        {
            AgentRuntimeConversationFactoryLog.AgentRunFailed(
                logger,
                exception,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                false);
            throw;
        }
    }

    private async IAsyncEnumerable<AgentResponseUpdate> LogAgentRunStreamingAsync(
        AgentExecutionDescriptor descriptor,
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        var runContext = ResolveRunContext(descriptor, options);
        using var scope = BeginScope(runContext);
        var stopwatch = Stopwatch.StartNew();
        var updateCount = 0;
        var totalCharacters = 0;
        var loggedMessageId = false;
        var isInfoEnabled = logger.IsEnabled(LogLevel.Information);

        if (isInfoEnabled)
        {
            AgentRuntimeConversationFactoryLog.AgentRunStarted(
                logger,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                runContext.AgentName,
                runContext.ProviderKind,
                true,
                materializedMessages.Count);
        }

        await using var enumerator = innerAgent.RunStreamingAsync(
                materializedMessages,
                session,
                options,
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            AgentResponseUpdate update;
            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                update = enumerator.Current;
            }
            catch (Exception exception)
            {
                AgentRuntimeConversationFactoryLog.AgentRunFailed(
                    logger,
                    exception,
                    runContext.RunId,
                    runContext.SessionId,
                    runContext.AgentId,
                    true);
                throw;
            }

            updateCount++;
            totalCharacters += update.Text?.Length ?? 0;

            if (isInfoEnabled && !loggedMessageId && !string.IsNullOrWhiteSpace(update.MessageId))
            {
                loggedMessageId = true;
                AgentRuntimeConversationFactoryLog.AgentRunFirstUpdateObserved(
                    logger,
                    runContext.RunId,
                    runContext.SessionId,
                    runContext.AgentId,
                    update.MessageId,
                    update.Text?.Length ?? 0);
            }

            yield return update;
        }

        if (isInfoEnabled)
        {
            AgentRuntimeConversationFactoryLog.AgentRunCompleted(
                logger,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                true,
                updateCount,
                totalCharacters,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
