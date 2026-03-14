using System.Globalization;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DotPilot.Runtime.Features.AgentSessions;

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

        var responseMessages = context.ResponseMessages ?? [];
        await chatHistoryStore.AppendAsync(
            storageKey,
            context.RequestMessages.Concat(responseMessages),
            cancellationToken);
    }

    private static string? GetStorageKey(AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var state = SessionState.GetOrInitializeState(session);
        return string.IsNullOrWhiteSpace(state.StorageKey) ? null : state.StorageKey;
    }
}

internal sealed class FolderChatHistoryState
{
    public string? StorageKey { get; set; }
}
