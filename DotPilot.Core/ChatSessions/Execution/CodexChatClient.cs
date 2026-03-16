using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using DotPilot.Core.ControlPlaneDomain;
using ManagedCode.CodexSharpSDK.Client;
using ManagedCode.CodexSharpSDK.Configuration;
using ManagedCode.CodexSharpSDK.Models;
using Microsoft.Extensions.AI;

namespace DotPilot.Core.ChatSessions;

internal sealed class CodexChatClient(
    SessionId sessionId,
    string agentName,
    string fallbackModelName,
    LocalCodexThreadStateStore threadStateStore,
    TimeProvider timeProvider) : IChatClient
{
    private const string ContinuePrompt = "Continue the conversation.";
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private CodexClient? _client;
    private CodexThread? _thread;
    private LocalCodexThreadState? _threadState;
    private bool _disposed;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = ResolveLatestUserPrompt(messages);
        var instructions = options?.Instructions ?? string.Empty;
        var turnContext = await EnsureThreadAsync(options?.ModelId, instructions, cancellationToken);
        var result = await turnContext.Thread.RunAsync(
            BuildInputs(prompt, instructions, turnContext.State.InstructionsSeeded),
            new TurnOptions
            {
                CancellationToken = cancellationToken,
            });

        await MarkInstructionsSeededAsync(turnContext.State, cancellationToken);

        var timestamp = timeProvider.GetUtcNow();
        var message = new ChatMessage(ChatRole.Assistant, ResolveResponseText(result))
        {
            AuthorName = agentName,
            CreatedAt = timestamp,
            MessageId = Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture),
        };

        return new ChatResponse(message)
        {
            CreatedAt = timestamp,
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prompt = ResolveLatestUserPrompt(messages);
        var instructions = options?.Instructions ?? string.Empty;
        var turnContext = await EnsureThreadAsync(options?.ModelId, instructions, cancellationToken);
        var streamed = await turnContext.Thread.RunStreamedAsync(
            BuildInputs(prompt, instructions, turnContext.State.InstructionsSeeded),
            new TurnOptions
            {
                CancellationToken = cancellationToken,
            });

        await MarkInstructionsSeededAsync(turnContext.State, cancellationToken);

        var messageId = Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);
        Dictionary<string, int> observedTextLengths = [];

        await foreach (var evt in streamed.Events.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (evt is TurnFailedEvent failed)
            {
                throw new InvalidOperationException(failed.Error.Message);
            }

            if (!TryCreateAssistantUpdate(evt, observedTextLengths, messageId, out var update))
            {
                continue;
            }

            update.AuthorName = agentName;
            update.CreatedAt = timeProvider.GetUtcNow();
            yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(IChatClient) ? this : null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _thread?.Dispose();
        _client?.Dispose();
        _initializationGate.Dispose();
        _disposed = true;
    }

    private async Task<CodexTurnContext> EnsureThreadAsync(
        string? requestedModelName,
        string systemInstructions,
        CancellationToken cancellationToken)
    {
        var normalizedModelName = string.IsNullOrWhiteSpace(requestedModelName)
            ? fallbackModelName
            : requestedModelName.Trim();
        var normalizedPromptHash = ComputeHash(systemInstructions);

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (_thread is not null && _threadState is not null)
            {
                return new CodexTurnContext(_thread, _threadState);
            }

            _client ??= new CodexClient(new CodexOptions());

            var persistedState = await threadStateStore.TryLoadAsync(sessionId, cancellationToken);
            var workingDirectory = ResolveWorkingDirectory(persistedState);
            Directory.CreateDirectory(workingDirectory);

            if (CanResumeThread(persistedState, normalizedModelName, normalizedPromptHash))
            {
                var resumableState = persistedState!;
                try
                {
                    _thread = _client.ResumeThread(
                        resumableState.ThreadId,
                        CreateThreadOptions(normalizedModelName, workingDirectory));
                    _threadState = resumableState;
                    return new CodexTurnContext(_thread, _threadState);
                }
                catch
                {
                }
            }

            _thread = _client.StartThread(CreateThreadOptions(normalizedModelName, workingDirectory));
            _threadState = new LocalCodexThreadState(
                _thread.Id ?? Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture),
                workingDirectory,
                normalizedModelName,
                normalizedPromptHash,
                InstructionsSeeded: false);
            await threadStateStore.SaveAsync(sessionId, _threadState, cancellationToken);
            return new CodexTurnContext(_thread, _threadState);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task MarkInstructionsSeededAsync(
        LocalCodexThreadState state,
        CancellationToken cancellationToken)
    {
        if (state.InstructionsSeeded)
        {
            return;
        }

        var updatedState = state with
        {
            InstructionsSeeded = true,
        };

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            _threadState = updatedState;
            await threadStateStore.SaveAsync(sessionId, updatedState, cancellationToken);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private string ResolveWorkingDirectory(LocalCodexThreadState? persistedState)
    {
        return string.IsNullOrWhiteSpace(persistedState?.WorkingDirectory)
            ? threadStateStore.ResolvePlaygroundDirectory(sessionId)
            : persistedState.WorkingDirectory;
    }

    private static bool CanResumeThread(
        LocalCodexThreadState? persistedState,
        string modelName,
        string systemPromptHash)
    {
        return persistedState is not null &&
            !string.IsNullOrWhiteSpace(persistedState.ThreadId) &&
            string.Equals(persistedState.ModelName, modelName, StringComparison.Ordinal) &&
            string.Equals(persistedState.SystemPromptHash, systemPromptHash, StringComparison.Ordinal);
    }

    private static ThreadOptions CreateThreadOptions(string modelName, string workingDirectory)
    {
        return new ThreadOptions
        {
            ApprovalPolicy = ApprovalMode.Never,
            Model = modelName,
            SandboxMode = SandboxMode.WorkspaceWrite,
            SkipGitRepoCheck = true,
            WorkingDirectory = workingDirectory,
        };
    }

    private static IReadOnlyList<UserInput> BuildInputs(
        string prompt,
        string systemInstructions,
        bool instructionsSeeded)
    {
        var text = instructionsSeeded || string.IsNullOrWhiteSpace(systemInstructions)
            ? prompt
            : string.Create(
                CultureInfo.InvariantCulture,
                $$"""
                Follow these system instructions for this entire session:
                {{systemInstructions.Trim()}}

                Operator request:
                {{prompt}}
                """);

        return [new TextInput(text)];
    }

    private static string ResolveLatestUserPrompt(IEnumerable<ChatMessage> messages)
    {
        var prompt = messages
            .LastOrDefault(static message => message.Role == ChatRole.User)
            ?.Text
            ?.Trim();

        return string.IsNullOrWhiteSpace(prompt) ? ContinuePrompt : prompt;
    }

    private static string ResolveResponseText(RunResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.FinalResponse))
        {
            return result.FinalResponse;
        }

        return result.Items
            .OfType<AgentMessageItem>()
            .Select(static item => item.Text)
            .LastOrDefault(static text => !string.IsNullOrWhiteSpace(text))
            ?? string.Empty;
    }

    private static bool TryCreateAssistantUpdate(
        ThreadEvent evt,
        Dictionary<string, int> observedTextLengths,
        string messageId,
        out ChatResponseUpdate update)
    {
        update = null!;
        var item = evt switch
        {
            ItemUpdatedEvent updated => updated.Item,
            ItemCompletedEvent completed => completed.Item,
            _ => null,
        };

        if (item is not AgentMessageItem assistantMessageItem)
        {
            return false;
        }

        var textKey = string.IsNullOrWhiteSpace(assistantMessageItem.Id)
            ? messageId
            : assistantMessageItem.Id;
        var currentText = assistantMessageItem.Text ?? string.Empty;
        observedTextLengths.TryGetValue(textKey, out var knownLength);
        if (currentText.Length <= knownLength)
        {
            return false;
        }

        observedTextLengths[textKey] = currentText.Length;
        update = new ChatResponseUpdate(ChatRole.Assistant, currentText[knownLength..])
        {
            MessageId = messageId,
        };
        return true;
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private readonly record struct CodexTurnContext(CodexThread Thread, LocalCodexThreadState State);
}
