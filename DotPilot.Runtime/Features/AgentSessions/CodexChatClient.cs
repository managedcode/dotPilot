using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using ManagedCode.CodexSharpSDK.Client;
using ManagedCode.CodexSharpSDK.Configuration;
using ManagedCode.CodexSharpSDK.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class CodexChatClient : IChatClient
{
    private const string InstructionsHeading = "System instructions";
    private const string ConversationHeading = "Conversation";
    private const string EmptyConversationText = "No conversation history was provided.";
    private const string AssistantRoleLabel = "Assistant";
    private const string UserRoleLabel = "User";
    private const string SystemRoleLabel = "System";
    private const string UnknownRoleLabel = "Message";
    private readonly string _agentName;
    private readonly TimeProvider _timeProvider;
    private readonly CodexClient _client;
    private readonly ThreadOptions _baseThreadOptions;

    public CodexChatClient(
        string agentName,
        string modelName,
        IReadOnlyList<string> capabilities,
        string? executablePath,
        string workingDirectory,
        TimeProvider timeProvider,
        ILogger<CodexChatClient> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _agentName = agentName;
        _timeProvider = timeProvider;
        _baseThreadOptions = CreateBaseThreadOptions(modelName, capabilities, workingDirectory);
        _client = new CodexClient(
            new CodexClientOptions
            {
                CodexOptions = new CodexOptions
                {
                    CodexExecutablePath = executablePath,
                    Logger = logger,
                },
            });

        _client
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(messages, options);

        using var thread = _client.StartThread(ResolveThreadOptions(options));
        var result = await thread.RunAsync(
            prompt,
            new TurnOptions
            {
                CancellationToken = cancellationToken,
            });

        var timestamp = _timeProvider.GetUtcNow();
        var message = new ChatMessage(ChatRole.Assistant, ResolveResponseText(result))
        {
            AuthorName = _agentName,
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
        var prompt = BuildPrompt(messages, options);

        using var thread = _client.StartThread(ResolveThreadOptions(options));
        var runResult = await thread.RunStreamedAsync(
            prompt,
            new TurnOptions
            {
                CancellationToken = cancellationToken,
            });

        string? latestText = null;
        string? messageId = null;

        await foreach (var threadEvent in runResult.Events.WithCancellation(cancellationToken))
        {
            switch (threadEvent)
            {
                case ItemUpdatedEvent { Item: AgentMessageItem item }:
                    var updatedMessage = CreateUpdate(item, ref latestText, ref messageId);
                    if (updatedMessage is not null)
                    {
                        yield return updatedMessage;
                    }

                    break;

                case ItemCompletedEvent { Item: AgentMessageItem item }:
                    var completedMessage = CreateUpdate(item, ref latestText, ref messageId);
                    if (completedMessage is not null)
                    {
                        yield return completedMessage;
                    }

                    break;

                case TurnFailedEvent failedEvent:
                    throw new InvalidOperationException(failedEvent.Error.Message);

                case ThreadErrorEvent errorEvent:
                    throw new InvalidOperationException(errorEvent.Message);
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        _ = serviceKey;
        return serviceType == typeof(IChatClient) ? this : null;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static ThreadOptions CreateBaseThreadOptions(
        string modelName,
        IReadOnlyList<string> capabilities,
        string workingDirectory)
    {
        var tools = AgentSessionDefaults.DecodeTools(capabilities);
        return new ThreadOptions
        {
            Model = modelName,
            ModelReasoningEffort = ModelReasoningEffort.Medium,
            WorkingDirectory = workingDirectory,
            SkipGitRepoCheck = true,
            ApprovalPolicy = ApprovalMode.Never,
            SandboxMode = RequiresWorkspaceWrite(tools)
                ? SandboxMode.WorkspaceWrite
                : SandboxMode.ReadOnly,
            WebSearchEnabled = tools.Contains(AgentSessionDefaults.WebCapability, StringComparer.Ordinal),
        };
    }

    private static bool RequiresWorkspaceWrite(IReadOnlyList<string> tools)
    {
        return tools.Contains(AgentSessionDefaults.FilesCapability, StringComparer.Ordinal) ||
            tools.Contains(AgentSessionDefaults.ShellCapability, StringComparer.Ordinal);
    }

    private ThreadOptions ResolveThreadOptions(ChatOptions? options)
    {
        return string.IsNullOrWhiteSpace(options?.ModelId)
            ? _baseThreadOptions
            : _baseThreadOptions with
            {
                Model = options.ModelId,
            };
    }

    private ChatResponseUpdate? CreateUpdate(
        AgentMessageItem item,
        ref string? latestText,
        ref string? messageId)
    {
        messageId ??= item.Id;
        var delta = ExtractDelta(latestText, item.Text);
        latestText = item.Text;
        if (string.IsNullOrWhiteSpace(delta))
        {
            return null;
        }

        return new ChatResponseUpdate(ChatRole.Assistant, delta)
        {
            AuthorName = _agentName,
            CreatedAt = _timeProvider.GetUtcNow(),
            MessageId = messageId,
        };
    }

    private static string ExtractDelta(string? previousText, string currentText)
    {
        if (string.IsNullOrEmpty(previousText))
        {
            return currentText;
        }

        return currentText.StartsWith(previousText, StringComparison.Ordinal)
            ? currentText[previousText.Length..]
            : currentText;
    }

    private static string ResolveResponseText(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.IsNullOrWhiteSpace(result.FinalResponse))
        {
            return result.FinalResponse;
        }

        return result.Items
            .OfType<AgentMessageItem>()
            .LastOrDefault()
            ?.Text ??
            string.Empty;
    }

    private static string BuildPrompt(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        ArgumentNullException.ThrowIfNull(messages);

        StringBuilder builder = new();
        if (!string.IsNullOrWhiteSpace(options?.Instructions))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{InstructionsHeading}:");
            builder.AppendLine(options.Instructions.Trim());
            builder.AppendLine();
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"{ConversationHeading}:");

        var contentWritten = false;
        foreach (var message in messages)
        {
            var formattedMessage = FormatMessage(message);
            if (string.IsNullOrWhiteSpace(formattedMessage))
            {
                continue;
            }

            contentWritten = true;
            builder.AppendLine(formattedMessage);
            builder.AppendLine();
        }

        if (!contentWritten)
        {
            builder.AppendLine(EmptyConversationText);
        }

        return builder.ToString().Trim();
    }

    private static string FormatMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            [
                ResolveRoleLabel(message) + ":",
                text,
            ]);
    }

    private static string ResolveRoleLabel(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.AuthorName))
        {
            return message.AuthorName;
        }

        if (message.Role == ChatRole.User)
        {
            return UserRoleLabel;
        }

        if (message.Role == ChatRole.Assistant)
        {
            return AssistantRoleLabel;
        }

        if (message.Role == ChatRole.System)
        {
            return SystemRoleLabel;
        }

        return UnknownRoleLabel;
    }
}
