using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DotPilot.Core.ChatSessions;

internal sealed class FolderChatHistoryProvider(LocalAgentChatHistoryStore chatHistoryStore)
    : ChatHistoryProvider(
        provideOutputMessageFilter: static messages => messages,
        storeInputRequestMessageFilter: static messages => messages,
        storeInputResponseMessageFilter: static messages => messages)
{
    private const string ProviderStateKey = "DotPilot.AgentSessionHistory";
    private static readonly ProviderSessionState<FolderChatHistoryState> SessionState = new(
        static _ => new FolderChatHistoryState(),
        ProviderStateKey,
        AgentSessionSerialization.Options);

    public static void BindToSession(AgentSession session, SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(session);

        var state = SessionState.GetOrInitializeState(session);
        state.StorageKey = sessionId.Value.ToString("N", CultureInfo.InvariantCulture);
        SessionState.SaveState(session, state);
    }

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is null)
        {
            return [];
        }

        var storageKey = GetStorageKey(context.Session);
        return storageKey is null
            ? []
            : await chatHistoryStore.LoadAsync(storageKey, cancellationToken);
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context,
        CancellationToken cancellationToken)
    {
        if (context.Session is null)
        {
            return;
        }

        var storageKey = GetStorageKey(context.Session);
        if (storageKey is null)
        {
            return;
        }

        var existing = await chatHistoryStore.LoadAsync(storageKey, cancellationToken);
        var knownMessageKeys = existing
            .Select(CreateMessageKey)
            .ToHashSet(StringComparer.Ordinal);
        var responseMessages = context.ResponseMessages ?? [];
        var newMessages = context.RequestMessages
            .Concat(responseMessages)
            .Where(message => knownMessageKeys.Add(CreateMessageKey(message)))
            .ToArray();
        if (newMessages.Length == 0)
        {
            return;
        }

        await chatHistoryStore.AppendAsync(
            storageKey,
            newMessages,
            cancellationToken);
    }

    private static string? GetStorageKey(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var state = SessionState.GetOrInitializeState(session);
        return string.IsNullOrWhiteSpace(state.StorageKey) ? null : state.StorageKey;
    }

    private static string CreateMessageKey(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return string.IsNullOrWhiteSpace(message.MessageId)
            ? string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0}|{1}|{2:O}|{3}",
                message.Role,
                message.AuthorName,
                message.CreatedAt,
                message.Text)
            : message.MessageId;
    }
}
