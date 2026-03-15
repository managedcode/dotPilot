using System.Collections.Immutable;
using System.Globalization;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public partial record MainModel(
    IAgentWorkspaceState workspaceState,
    ILogger<MainModel> logger)
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
    private AsyncCommand? _startNewSessionCommand;
    private AsyncCommand? _submitMessageCommand;
    private readonly Signal _workspaceRefresh = new();
    private readonly Signal _sessionRefresh = new();

    public string ComposerPlaceholder => DefaultComposerPlaceholder;

    public IState<string> ComposerText => State.Value(this, static () => string.Empty);

    public IState<string> FeedbackMessage => State.Value(this, static () => string.Empty);

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
            MainViewModelLog.RefreshRequested(logger);
            await workspaceState.RefreshWorkspaceAsync(cancellationToken);
            _workspaceRefresh.Raise();
            _sessionRefresh.Raise();
            await EnsureSelectedChatAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask StartNewSession(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (workspace.Agents.Count == 0)
            {
                await FeedbackMessage.SetAsync(StartSessionValidationMessage, cancellationToken);
                return;
            }

            MainViewModelLog.StartingSession(logger);
            var agent = workspace.Agents[0];
            var session = await workspaceState.CreateSessionAsync(
                new CreateSessionCommand($"Session with {agent.Name}", agent.Id),
                cancellationToken);

            await SelectedChat.UpdateAsync(_ => MapSidebarItem(session.Session), cancellationToken);
            await FeedbackMessage.SetAsync(string.Empty, cancellationToken);
            _workspaceRefresh.Raise();
            _sessionRefresh.Raise();
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(logger, exception);
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

    private async ValueTask SendMessageCore(string? messageOverride, CancellationToken cancellationToken)
    {
        var message = string.IsNullOrWhiteSpace(messageOverride)
            ? ((await ComposerText) ?? string.Empty).Trim()
            : messageOverride.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            MainViewModelLog.SendIgnoredEmpty(logger);
            return;
        }

        if (!await HasAgents)
        {
            MainViewModelLog.SendIgnoredNoAgents(logger);
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
                MainViewModelLog.SendRequested(logger, sessionId, message.Length);
                BrowserConsoleDiagnostics.Info($"[DotPilot.Chat] Send requested. SessionId={sessionId} CharacterCount={message.Length}");
            }

            await foreach (var _ in workspaceState.SendMessageAsync(
                               new SendSessionMessageCommand(selectedChat.Id, message),
                               cancellationToken))
            {
                _workspaceRefresh.Raise();
                _sessionRefresh.Raise();
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                var sessionId = selectedChat.Id.Value.ToString("N", CultureInfo.InvariantCulture);
                MainViewModelLog.SendCompleted(logger, sessionId);
                BrowserConsoleDiagnostics.Info($"[DotPilot.Chat] Send completed. SessionId={sessionId}");
            }

            await FeedbackMessage.SetAsync(string.Empty, cancellationToken);
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(logger, exception);
            await FeedbackMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask<IImmutableList<SessionSidebarItem>> LoadRecentChatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            MainViewModelLog.LoadingWorkspace(logger);
            var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
            MainViewModelLog.WorkspaceLoaded(logger, workspace.Sessions.Count, workspace.Agents.Count);
            var sessions = workspace.Sessions
                .Select(MapSidebarItem)
                .ToImmutableArray();
            await EnsureSelectedChatAsync(workspace, sessions, cancellationToken);
            return sessions;
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(logger, exception);
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
        var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
        return workspace.Agents.Count > 0;
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
            : await workspaceState.GetSessionAsync(selectedChat.Id, cancellationToken);
    }

    private async ValueTask EnsureSelectedChatAsync(CancellationToken cancellationToken)
    {
        var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
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
            ResolveAgentBrush(agent.ProviderKind),
            agent.Role.ToString(),
            DesignBrushPalette.BadgeSurfaceBrush);
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
