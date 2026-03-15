using System.Collections.Immutable;
using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record ChatModel
{
    private const string EmptyTitleValue = "No active session";
    private const string EmptyStatusValue = "A default system agent is ready. Start a session or create another agent.";
    private const string ReadyToStartStatusValue = "Start a session or send the first message.";
    private const string DefaultComposerPlaceholder = "Message your local agent session";
    private const string SendInProgressMessage = "Sending message...";
    private const string StartSessionValidationMessage = "Create an agent before starting a session.";
    private const string LocalMemberName = "Local operator";
    private const string LocalMemberSummary = "This desktop instance";
    private static readonly SessionSidebarItem EmptySelectedChat = new(default, string.Empty, string.Empty);
    private static readonly ParticipantItem LocalMember = new(
        LocalMemberName,
        LocalMemberSummary,
        "L",
        DesignBrushPalette.UserAvatarBrush);
    private readonly IAgentWorkspaceState workspaceState;
    private readonly IOperatorPreferencesStore operatorPreferencesStore;
    private readonly ILogger<ChatModel> logger;
    private AsyncCommand? _startNewSessionCommand;
    private AsyncCommand? _submitMessageCommand;
    private readonly Signal _workspaceRefresh = new();
    private readonly Signal _sessionRefresh = new();

    public ChatModel(
        IAgentWorkspaceState workspaceState,
        IOperatorPreferencesStore operatorPreferencesStore,
        WorkspaceProjectionNotifier workspaceProjectionNotifier,
        ILogger<ChatModel> logger)
    {
        this.workspaceState = workspaceState;
        this.operatorPreferencesStore = operatorPreferencesStore;
        this.logger = logger;
        workspaceProjectionNotifier.Changed += OnWorkspaceProjectionChanged;
    }

    public string ComposerPlaceholder => DefaultComposerPlaceholder;

    public IState<string> ComposerText => State.Value(this, static () => string.Empty);

    public IState<string> FeedbackMessage => State.Value(this, static () => string.Empty);

    public IState<ComposerSendBehavior> ComposerSendBehavior => State.Async(this, LoadComposerSendBehaviorAsync, _workspaceRefresh);

    public IState<string> ComposerSendHint => State.Async(this, LoadComposerSendHintAsync, _workspaceRefresh);

    public IState<SessionSidebarItem> SelectedChat => State.Value(this, static () => EmptySelectedChat);

    public IListState<SessionSidebarItem> RecentChats => ListState.Async(this, LoadRecentChatsAsync, _workspaceRefresh);

    public IState<ChatSessionView> ActiveSession => State.Async(this, LoadActiveSessionViewAsync, _sessionRefresh);

    public IState<bool> HasAgents => State.Async(this, LoadHasAgentsAsync, _workspaceRefresh);

    public IState<bool> CanSend => State.Async(this, LoadCanSendAsync);

    public ICommand StartNewSessionCommand =>
        _startNewSessionCommand ??= new AsyncCommand(
            () => StartNewSession(CancellationToken.None));

    public ICommand SubmitMessageCommand =>
        _submitMessageCommand ??= new AsyncCommand(
            parameter => SendMessageCore(parameter as string, CancellationToken.None));

    public async ValueTask Refresh(CancellationToken cancellationToken)
    {
        try
        {
            ChatModelLog.RefreshRequested(logger);
            var refresh = await workspaceState.RefreshWorkspaceAsync(cancellationToken);
            if (refresh.IsFailed)
            {
                await FeedbackMessage.SetAsync(refresh.ToOperatorMessage("Could not refresh workspace."), cancellationToken);
                return;
            }

            _workspaceRefresh.Raise();
            _sessionRefresh.Raise();
            await EnsureSelectedChatAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            ChatModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask StartNewSession(CancellationToken cancellationToken)
    {
        try
        {
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await FeedbackMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load workspace."), cancellationToken);
                return;
            }

            if (workspace.Agents.Count == 0)
            {
                await FeedbackMessage.SetAsync(StartSessionValidationMessage, cancellationToken);
                return;
            }

            ChatModelLog.StartingSession(logger);
            var agent = workspace.Agents[0];
            var sessionResult = await workspaceState.CreateSessionAsync(
                new CreateSessionCommand($"Session with {agent.Name}", agent.Id),
                cancellationToken);
            if (!sessionResult.TryGetValue(out var session))
            {
                await FeedbackMessage.SetAsync(sessionResult.ToOperatorMessage("Could not start a session."), cancellationToken);
                return;
            }

            await SelectedChat.UpdateAsync(_ => MapSidebarItem(session.Session), cancellationToken);
            await FeedbackMessage.SetAsync(string.Empty, cancellationToken);
            _workspaceRefresh.Raise();
            _sessionRefresh.Raise();
        }
        catch (Exception exception)
        {
            ChatModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public ValueTask SendMessage(CancellationToken cancellationToken)
    {
        return SendMessageCore(messageOverride: null, cancellationToken);
    }

    public ValueTask SubmitMessage(CancellationToken cancellationToken)
    {
        return SendMessageCore(messageOverride: null, cancellationToken);
    }

    private void OnWorkspaceProjectionChanged(object? sender, EventArgs e)
    {
        _workspaceRefresh.Raise();
        _sessionRefresh.Raise();
    }

    private async ValueTask SendMessageCore(string? messageOverride, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(messageOverride)
            ? ((await ComposerText) ?? string.Empty).Trim()
            : messageOverride.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            ChatModelLog.SendIgnoredEmpty(logger);
            return;
        }

        if (!await HasAgents)
        {
            ChatModelLog.SendIgnoredNoAgents(logger);
            await FeedbackMessage.SetAsync(StartSessionValidationMessage, cancellationToken);
            return;
        }

        await ComposerText.SetAsync(string.Empty, cancellationToken);
        await FeedbackMessage.SetAsync(SendInProgressMessage, cancellationToken);

        try
        {
            var selectedChat = (await SelectedChat) ?? EmptySelectedChat;
            if (IsEmptySelectedChat(selectedChat))
            {
                await StartNewSession(cancellationToken);
                selectedChat = (await SelectedChat) ?? EmptySelectedChat;
                if (IsEmptySelectedChat(selectedChat))
                {
                    return;
                }
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                var sessionId = selectedChat.Id.Value.ToString("N", CultureInfo.InvariantCulture);
                ChatModelLog.SendRequested(logger, sessionId, message.Length);
                BrowserConsoleDiagnostics.Info($"[DotPilot.Chat] Send requested. SessionId={sessionId} CharacterCount={message.Length}");
            }

            var sendFailed = false;
            await foreach (var _ in workspaceState.SendMessageAsync(
                               new SendSessionMessageCommand(selectedChat.Id, message),
                               cancellationToken))
            {
                if (_.IsFailed)
                {
                    await FeedbackMessage.SetAsync(_.ToOperatorMessage("Message send failed."), cancellationToken);
                    sendFailed = true;
                    break;
                }

                _workspaceRefresh.Raise();
                _sessionRefresh.Raise();
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                var sessionId = selectedChat.Id.Value.ToString("N", CultureInfo.InvariantCulture);
                ChatModelLog.SendCompleted(logger, sessionId);
                BrowserConsoleDiagnostics.Info($"[DotPilot.Chat] Send completed. SessionId={sessionId}");
            }

            if (!sendFailed)
            {
                await FeedbackMessage.SetAsync(string.Empty, cancellationToken);
            }
        }
        catch (Exception exception)
        {
            ChatModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask<IImmutableList<SessionSidebarItem>> LoadRecentChatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ChatModelLog.LoadingWorkspace(logger);
            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await FeedbackMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not load sessions."), cancellationToken);
                return ImmutableArray<SessionSidebarItem>.Empty;
            }

            ChatModelLog.WorkspaceLoaded(logger, workspace.Sessions.Count, workspace.Agents.Count);
            var sessions = workspace.Sessions
                .Select(MapSidebarItem)
                .ToImmutableArray();
            await EnsureSelectedChatAsync(workspace, sessions, cancellationToken);
            return sessions;
        }
        catch (Exception exception)
        {
            ChatModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
            return ImmutableArray<SessionSidebarItem>.Empty;
        }
    }

    private async ValueTask<ChatSessionView> LoadActiveSessionViewAsync(CancellationToken cancellationToken)
    {
        var snapshot = await LoadActiveSessionAsync(cancellationToken);
        if (snapshot is null)
        {
            return new ChatSessionView(
                "S",
                EmptyTitleValue,
                await HasAgents ? ReadyToStartStatusValue : EmptyStatusValue,
                [],
                [LocalMember],
                []);
        }

        return new ChatSessionView(
            GetInitial(snapshot.Session.Title),
            snapshot.Session.Title,
            $"{snapshot.Session.PrimaryAgentName} · {snapshot.Session.ProviderDisplayName}",
            snapshot.Entries
                .Select(MapTimelineItem)
                .ToImmutableArray(),
            [LocalMember],
            snapshot.Participants
                .Select(MapParticipant)
                .ToImmutableArray());
    }

    private async ValueTask<bool> LoadHasAgentsAsync(CancellationToken cancellationToken)
    {
        var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
        if (!workspaceResult.TryGetValue(out var workspace))
        {
            return false;
        }

        return workspace.Agents.Count > 0;
    }

    private async ValueTask<ComposerSendBehavior> LoadComposerSendBehaviorAsync(CancellationToken cancellationToken)
    {
        var preferences = await operatorPreferencesStore.GetAsync(cancellationToken);
        return preferences.ComposerSendBehavior;
    }

    private async ValueTask<string> LoadComposerSendHintAsync(CancellationToken cancellationToken)
    {
        return ChatComposerSendBehaviorText.GetHint(await LoadComposerSendBehaviorAsync(cancellationToken));
    }

    private async ValueTask<bool> LoadCanSendAsync(CancellationToken cancellationToken)
    {
        var composerText = (await ComposerText) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(composerText) && await HasAgents;
    }

    private async ValueTask<SessionTranscriptSnapshot?> LoadActiveSessionAsync(CancellationToken cancellationToken)
    {
        await EnsureSelectedChatAsync(cancellationToken);
        var selectedChat = (await SelectedChat) ?? EmptySelectedChat;
        return IsEmptySelectedChat(selectedChat)
            ? null
            : (await workspaceState.GetSessionAsync(selectedChat.Id, cancellationToken)).TryGetValue(out var session)
                ? session
                : null;
    }

    private async ValueTask EnsureSelectedChatAsync(CancellationToken cancellationToken)
    {
        var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
        if (!workspaceResult.TryGetValue(out var workspace))
        {
            return;
        }

        var sessions = workspace.Sessions
            .Select(MapSidebarItem)
            .ToImmutableArray();
        await EnsureSelectedChatAsync(workspace, sessions, cancellationToken);
    }

    private async ValueTask EnsureSelectedChatAsync(
        AgentWorkspaceSnapshot workspace,
        IImmutableList<SessionSidebarItem> sessions,
        CancellationToken cancellationToken)
    {
        var selectedChat = (await SelectedChat) ?? EmptySelectedChat;
        var resolvedSelection = FindSessionById(sessions, selectedChat.Id);
        if (IsEmptySelectedChat(resolvedSelection) && workspace.SelectedSessionId is { } selectedSessionId)
        {
            resolvedSelection = FindSessionById(sessions, selectedSessionId);
        }

        if (IsEmptySelectedChat(resolvedSelection) && sessions.Count > 0)
        {
            resolvedSelection = sessions[0];
        }

        if (!Equals(selectedChat, resolvedSelection))
        {
            await SelectedChat.UpdateAsync(_ => resolvedSelection, cancellationToken);
        }
    }

    private static SessionSidebarItem FindSessionById(IImmutableList<SessionSidebarItem> sessions, SessionId sessionId)
    {
        for (var index = 0; index < sessions.Count; index++)
        {
            if (sessions[index].Id == sessionId)
            {
                return sessions[index];
            }
        }

        return EmptySelectedChat;
    }

    private static bool IsEmptySelectedChat(SessionSidebarItem? item)
    {
        return item is null || item.Id == default;
    }

    private static SessionSidebarItem MapSidebarItem(SessionListItem session)
    {
        return new SessionSidebarItem(session.Id, session.Title, session.Preview);
    }

    private static ParticipantItem MapParticipant(AgentProfileSummary agent)
    {
        return new ParticipantItem(
            agent.Name,
            $"{agent.ProviderDisplayName} · {agent.ModelName}",
            GetInitial(agent.Name),
            ResolveAgentBrush(agent.ProviderKind));
    }

    private static ChatTimelineItem MapTimelineItem(SessionStreamEntry entry)
    {
        var isCurrentUser = entry.Kind == SessionStreamEntryKind.UserMessage;
        var author = entry.Author;
        var initial = GetInitial(author);
        var avatarBrush = ResolveTimelineBrush(entry);

        return new ChatTimelineItem(
            entry.Id,
            entry.Kind,
            author,
            entry.Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
            entry.Text,
            initial,
            avatarBrush,
            isCurrentUser,
            entry.AccentLabel);
    }

    private static string GetInitial(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "?"
            : value.Trim()[0].ToString(CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    private static Brush? ResolveTimelineBrush(SessionStreamEntry entry)
    {
        return entry.Kind switch
        {
            SessionStreamEntryKind.UserMessage => DesignBrushPalette.UserAvatarBrush,
            SessionStreamEntryKind.ToolStarted or SessionStreamEntryKind.ToolCompleted => DesignBrushPalette.AnalyticsAvatarBrush,
            SessionStreamEntryKind.Status => DesignBrushPalette.AvatarVariantEmilyBrush,
            SessionStreamEntryKind.Error => DesignBrushPalette.AvatarVariantFrankBrush,
            _ => DesignBrushPalette.CodeAvatarBrush,
        };
    }

    private static Brush? ResolveAgentBrush(AgentProviderKind providerKind)
    {
        return providerKind switch
        {
            AgentProviderKind.Debug => DesignBrushPalette.DesignAvatarBrush,
            AgentProviderKind.Codex => DesignBrushPalette.CodeAvatarBrush,
            AgentProviderKind.ClaudeCode => DesignBrushPalette.AnalyticsAvatarBrush,
            AgentProviderKind.GitHubCopilot => DesignBrushPalette.AvatarVariantDanishBrush,
            _ => DesignBrushPalette.CodeAvatarBrush,
        };
    }
}
