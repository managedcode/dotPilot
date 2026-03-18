using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed partial class AgentExecutionLoggingMiddleware
{
    private IChatClient WrapChatClient(IChatClient chatClient, AgentRunLogContext runContext)
    {
        ArgumentNullException.ThrowIfNull(chatClient);

        var bridgedChatClient = ShouldBridgeSystemInstructions(runContext.ProviderKind)
            ? chatClient
                .AsBuilder()
                .Use(
                    getResponseFunc: BridgeInstructionsAsync,
                    getStreamingResponseFunc: BridgeStreamingInstructionsAsync)
                .Build()
            : chatClient;

        return bridgedChatClient
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

    private static bool ShouldBridgeSystemInstructions(AgentProviderKind providerKind)
    {
        return providerKind is AgentProviderKind.Codex or AgentProviderKind.ClaudeCode or AgentProviderKind.Gemini;
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

    private static Task<ChatResponse> BridgeInstructionsAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        var bridgedRequest = CreateBridgedInstructionRequest(materializedMessages, options);
        return innerChatClient.GetResponseAsync(
            bridgedRequest.Messages,
            bridgedRequest.Options,
            cancellationToken);
    }

    private static IAsyncEnumerable<ChatResponseUpdate> BridgeStreamingInstructionsAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerChatClient,
        CancellationToken cancellationToken)
    {
        var materializedMessages = MaterializeMessages(messages);
        var bridgedRequest = CreateBridgedInstructionRequest(materializedMessages, options);
        return innerChatClient.GetStreamingResponseAsync(
            bridgedRequest.Messages,
            bridgedRequest.Options,
            cancellationToken);
    }

    private static BridgedInstructionRequest CreateBridgedInstructionRequest(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options)
    {
        if (options is null || string.IsNullOrWhiteSpace(options.Instructions))
        {
            return new BridgedInstructionRequest(messages, options);
        }

        var bridgedOptions = options.Clone();
        var instructions = bridgedOptions.Instructions!.Trim();
        bridgedOptions.Instructions = null;

        if (messages.Any(message =>
                message.Role == ChatRole.System &&
                string.Equals(message.Text?.Trim(), instructions, StringComparison.Ordinal)))
        {
            return new BridgedInstructionRequest(messages, bridgedOptions);
        }

        var bridgedMessages = new ChatMessage[messages.Count + 1];
        bridgedMessages[0] = new ChatMessage(ChatRole.System, instructions);
        for (var index = 0; index < messages.Count; index++)
        {
            bridgedMessages[index + 1] = messages[index];
        }

        return new BridgedInstructionRequest(bridgedMessages, bridgedOptions);
    }

    private sealed record BridgedInstructionRequest(
        IReadOnlyList<ChatMessage> Messages,
        ChatOptions? Options);
}
