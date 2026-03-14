using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class DebugChatClient(string agentName, TimeProvider timeProvider) : IChatClient
{
    private const int ChunkDelayMilliseconds = 45;
    private const string FallbackPrompt = "the latest request";
    private const string Newline = "\n";

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var responseText = CreateResponseText(messages);
        var timestamp = timeProvider.GetUtcNow();
        var message = new ChatMessage(ChatRole.Assistant, responseText)
        {
            AuthorName = agentName,
            CreatedAt = timestamp,
            MessageId = Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
        };

        var response = new ChatResponse(message)
        {
            CreatedAt = timestamp,
        };

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var responseText = CreateResponseText(messages);
        var messageId = Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var chunk in SplitIntoChunks(responseText))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(ChunkDelayMilliseconds, cancellationToken);

            yield return new ChatResponseUpdate(ChatRole.Assistant, chunk)
            {
                AuthorName = agentName,
                CreatedAt = timeProvider.GetUtcNow(),
                MessageId = messageId,
            };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(IChatClient) ? this : null;
    }

    public void Dispose()
    {
    }

    private static IEnumerable<string> SplitIntoChunks(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunk = new List<string>(4);

        foreach (var word in words)
        {
            chunk.Add(word);
            if (chunk.Count < 4)
            {
                continue;
            }

            yield return string.Join(' ', chunk) + " ";
            chunk.Clear();
        }

        if (chunk.Count > 0)
        {
            yield return string.Join(' ', chunk);
        }
    }

    private static string CreateResponseText(IEnumerable<ChatMessage> messages)
    {
        var prompt = messages
            .LastOrDefault(message => message.Role == ChatRole.User)
            ?.Text
            ?.Trim();

        var effectivePrompt = string.IsNullOrWhiteSpace(prompt) ? FallbackPrompt : prompt;
        return string.Join(
            Newline,
            [
                $"Debug provider received: {effectivePrompt}",
                "This response is deterministic so the desktop shell and UI tests can validate streaming behavior.",
                "Tool activity is simulated inline before the final assistant answer completes.",
            ]);
    }
}

