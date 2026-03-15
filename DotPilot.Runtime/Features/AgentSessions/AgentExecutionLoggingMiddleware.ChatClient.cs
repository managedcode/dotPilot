using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed partial class AgentExecutionLoggingMiddleware
{
    private IChatClient WrapChatClient(IChatClient chatClient, AgentRunLogContext runContext)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        return chatClient
            .AsBuilder()
            .Use(
                getResponseFunc: (messages, options, innerChatClient, cancellationToken) =>
                    LogChatResponseAsync(
                        runContext,
                        messages,
                        options,
                        innerChatClient,
                        cancellationToken),
                getStreamingResponseFunc: (messages, options, innerChatClient, cancellationToken) =>
                    LogStreamingChatResponseAsync(
                        runContext,
                        messages,
                        options,
                        innerChatClient,
                        cancellationToken))
            .Build();
    }

    private async Task<ChatResponse> LogChatResponseAsync(
        AgentRunLogContext runContext,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        using var scope = BeginScope(runContext);
        var stopwatch = Stopwatch.StartNew();
        var isInfoEnabled = logger.IsEnabled(LogLevel.Information);

        if (isInfoEnabled)
        {
            var toolCount = CountTools(options);
            AgentRuntimeConversationFactoryLog.ChatClientRequestStarted(
                logger,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                false,
                runContext.ModelName,
                materializedMessages.Count,
                toolCount);
        }

        try
        {
            var response = await innerChatClient.GetResponseAsync(
                materializedMessages,
                options,
                cancellationToken);

            if (isInfoEnabled)
            {
                var outputCount = response.Messages.Count;
                var characterCount = CountMessageCharacters(response.Messages);
                AgentRuntimeConversationFactoryLog.ChatClientRequestCompleted(
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
            AgentRuntimeConversationFactoryLog.ChatClientRequestFailed(
                logger,
                exception,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                false);
            throw;
        }
    }

    private async IAsyncEnumerable<ChatResponseUpdate> LogStreamingChatResponseAsync(
        AgentRunLogContext runContext,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        using var scope = BeginScope(runContext);
        var stopwatch = Stopwatch.StartNew();
        var updateCount = 0;
        var totalCharacters = 0;
        var loggedMessageId = false;
        var isInfoEnabled = logger.IsEnabled(LogLevel.Information);

        if (isInfoEnabled)
        {
            var toolCount = CountTools(options);
            AgentRuntimeConversationFactoryLog.ChatClientRequestStarted(
                logger,
                runContext.RunId,
                runContext.SessionId,
                runContext.AgentId,
                true,
                runContext.ModelName,
                materializedMessages.Count,
                toolCount);
        }

        await using var enumerator = innerChatClient.GetStreamingResponseAsync(
                materializedMessages,
                options,
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            ChatResponseUpdate update;
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
                AgentRuntimeConversationFactoryLog.ChatClientRequestFailed(
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
                AgentRuntimeConversationFactoryLog.ChatClientFirstUpdateObserved(
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
            AgentRuntimeConversationFactoryLog.ChatClientRequestCompleted(
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
