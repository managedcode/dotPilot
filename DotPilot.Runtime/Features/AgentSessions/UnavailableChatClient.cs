using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class UnavailableChatClient(string message) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _ = messages;
        _ = options;
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(message);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _ = messages;
        _ = options;
        return ThrowAsync(cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        _ = serviceKey;
        return serviceType == typeof(IChatClient) ? this : null;
    }

    public void Dispose()
    {
    }

    private async IAsyncEnumerable<ChatResponseUpdate> ThrowAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException(message);
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
