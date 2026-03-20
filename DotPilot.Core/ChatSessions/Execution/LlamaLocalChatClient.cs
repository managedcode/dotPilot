using System.Runtime.CompilerServices;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotPilot.Core.ChatSessions;

internal sealed class LlamaLocalChatClient : IChatClient
{
    private readonly LLamaWeights weights;
    private readonly LLamaContext context;
    private readonly IChatClient innerChatClient;
    private bool disposed;

    public LlamaLocalChatClient(
        string modelPath,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var effectiveLogger = logger ?? NullLogger.Instance;
        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 4096,
        };

        weights = LLamaWeights.LoadFromFile(parameters);
        context = weights.CreateContext(parameters, effectiveLogger);
        var executor = new InteractiveExecutor(context, effectiveLogger);
        innerChatClient = executor.AsChatClient();
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return innerChatClient.GetResponseAsync(messages, options, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return StreamAsync(messages, options, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ThrowIfDisposed();
        return innerChatClient.GetService(serviceType, serviceKey);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        (innerChatClient as IDisposable)?.Dispose();
        context.Dispose();
        weights.Dispose();
        disposed = true;
    }

    private async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var update in innerChatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            yield return update;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
