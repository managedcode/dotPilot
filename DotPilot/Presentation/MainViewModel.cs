using System.Collections.ObjectModel;
using System.Globalization;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;

namespace DotPilot.Presentation;

[Bindable]
public sealed class MainViewModel : ObservableObject
{
    private const string EmptyTitleValue = "No active session";
    private const string EmptyStatusValue = "Create an agent, start a session, then chat from here.";
    private const string DefaultComposerPlaceholder = "Message your local agent session";
    private const string SendInProgressMessage = "Sending message to the local workflow...";
    private const string LocalMemberName = "Local operator";
    private const string LocalMemberSummary = "This desktop instance";
    private readonly IAgentSessionService _agentSessionService;
    private readonly IAgentProviderStatusCache _providerStatusCache;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly AsyncCommand _sendMessageCommand;
    private readonly AsyncCommand _startNewSessionCommand;
    private readonly AsyncCommand _refreshCommand;
    private readonly Dictionary<string, int> _timelineIndexById = new(StringComparer.Ordinal);
    private IReadOnlyList<AgentProfileSummary> _agents = [];
    private SessionSidebarItem? _selectedChat;
    private string _title = EmptyTitleValue;
    private string _statusSummary = EmptyStatusValue;
    private string _composerText = string.Empty;
    private string _feedbackMessage = string.Empty;

    public MainViewModel(
        IAgentSessionService agentSessionService,
        IAgentProviderStatusCache providerStatusCache,
        ILogger<MainViewModel> logger)
    {
        _agentSessionService = agentSessionService;
        _providerStatusCache = providerStatusCache;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread() ??
            throw new InvalidOperationException("MainViewModel requires a UI dispatcher queue.");
        RecentChats = [];
        Messages = [];
        Members = [new ParticipantItem(LocalMemberName, LocalMemberSummary, "L", DesignBrushPalette.UserAvatarBrush)];
        Agents = [];

        _sendMessageCommand = new AsyncCommand(SendMessageAsync, CanSendMessage);
        _startNewSessionCommand = new AsyncCommand(StartNewSessionAsync, CanStartNewSession);
        _refreshCommand = new AsyncCommand(RefreshWorkspaceAsync);

        _ = LoadWorkspaceAsync();
    }

    public ObservableCollection<SessionSidebarItem> RecentChats { get; }

    public ObservableCollection<ChatTimelineItem> Messages { get; }

    public ObservableCollection<ParticipantItem> Members { get; }

    public ObservableCollection<ParticipantItem> Agents { get; }

    public ICommand SendMessageCommand => _sendMessageCommand;

    public ICommand StartNewSessionCommand => _startNewSessionCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string StatusSummary
    {
        get => _statusSummary;
        private set => SetProperty(ref _statusSummary, value);
    }

    public string ComposerPlaceholder => DefaultComposerPlaceholder;

    public string ComposerText
    {
        get => _composerText;
        set
        {
            if (!SetProperty(ref _composerText, value))
            {
                return;
            }

            _sendMessageCommand.RaiseCanExecuteChanged();
        }
    }

    public string FeedbackMessage
    {
        get => _feedbackMessage;
        private set => SetProperty(ref _feedbackMessage, value);
    }

    public bool HasActiveSession => _selectedChat is not null;

    public bool HasAgents => _agents.Count > 0;

    public SessionSidebarItem? SelectedChat
    {
        get => _selectedChat;
        set
        {
            if (!SetProperty(ref _selectedChat, value))
            {
                return;
            }

            RaisePropertyChanged(nameof(HasActiveSession));
            _ = LoadSelectedSessionAsync();
        }
    }

    private async Task LoadWorkspaceAsync()
    {
        try
        {
            MainViewModelLog.LoadingWorkspace(_logger);
            var workspace = await _agentSessionService.GetWorkspaceAsync(CancellationToken.None);
            await RunOnUiThreadAsync(() =>
            {
                _agents = workspace.Agents;
                RebuildRecentChats(workspace.Sessions);
                _startNewSessionCommand.RaiseCanExecuteChanged();
                _sendMessageCommand.RaiseCanExecuteChanged();

                if (workspace.SelectedSessionId is { } selectedSessionId)
                {
                    SelectedChat = RecentChats.FirstOrDefault(chat => chat.Id == selectedSessionId);
                }
                else if (RecentChats.Count > 0)
                {
                    SelectedChat = RecentChats[0];
                }
                else
                {
                    ClearTimeline();
                    RebuildAgentPanel([]);
                    Title = EmptyTitleValue;
                    StatusSummary = HasAgents
                        ? "Start a new session from the sidebar or send the first message."
                        : EmptyStatusValue;
                }
            });

            MainViewModelLog.WorkspaceLoaded(_logger, workspace.Sessions.Count, workspace.Agents.Count);
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(_logger, exception);
            await RunOnUiThreadAsync(() => FeedbackMessage = exception.Message);
        }
    }

    private async Task LoadSelectedSessionAsync()
    {
        if (_selectedChat is null)
        {
            await RunOnUiThreadAsync(() =>
            {
                ClearTimeline();
                RebuildAgentPanel([]);
                Title = EmptyTitleValue;
                StatusSummary = HasAgents
                    ? "Start a new session from the sidebar or send the first message."
                    : EmptyStatusValue;
                _sendMessageCommand.RaiseCanExecuteChanged();
            });
            return;
        }

        var selectedChatId = _selectedChat.Id;
        var snapshot = await _agentSessionService.GetSessionAsync(selectedChatId, CancellationToken.None);
        if (snapshot is null)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            Title = snapshot.Session.Title;
            StatusSummary = $"{snapshot.Session.PrimaryAgentName} · {snapshot.Session.ProviderDisplayName}";
            RebuildTimeline(snapshot.Entries);
            RebuildAgentPanel(snapshot.Participants);
        });
    }

    private async Task RefreshWorkspaceAsync()
    {
        MainViewModelLog.RefreshRequested(_logger);
        await _providerStatusCache.RefreshAsync(CancellationToken.None);
        await LoadWorkspaceAsync();
    }

    private async Task StartNewSessionAsync()
    {
        if (_agents.Count == 0)
        {
            FeedbackMessage = "Create at least one agent before starting a session.";
            return;
        }

        MainViewModelLog.StartingSession(_logger);
        var agent = _agents[0];
        var session = await _agentSessionService.CreateSessionAsync(
            new CreateSessionCommand($"Session with {agent.Name}", agent.Id),
            CancellationToken.None);
        await RunOnUiThreadAsync(() => InsertOrUpdateRecentChat(session.Session, selectSession: true));
    }

    private async Task SendMessageAsync()
    {
        var message = ComposerText.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ComposerText = string.Empty;
        FeedbackMessage = SendInProgressMessage;
        var latestPreview = message;

        try
        {
            if (SelectedChat is null)
            {
                await StartNewSessionAsync();
                if (SelectedChat is null)
                {
                    return;
                }
            }

            var selectedChatId = SelectedChat.Id;
            await foreach (var entry in _agentSessionService.SendMessageAsync(
                               new SendSessionMessageCommand(selectedChatId, message),
                               CancellationToken.None))
            {
                if (entry.Kind is SessionStreamEntryKind.AssistantMessage && !string.IsNullOrWhiteSpace(entry.Text))
                {
                    latestPreview = entry.Text;
                }

                await RunOnUiThreadAsync(() => ApplyTimelineEntry(entry));
            }

            await RunOnUiThreadAsync(() =>
            {
                UpdateRecentChatPreview(selectedChatId, latestPreview ?? message);
                FeedbackMessage = string.Empty;
            });
        }
        catch (Exception exception)
        {
            MainViewModelLog.Failure(_logger, exception);
            await RunOnUiThreadAsync(() => FeedbackMessage = exception.Message);
        }
    }

    private bool CanSendMessage()
    {
        return !string.IsNullOrWhiteSpace(ComposerText) && HasAgents;
    }

    private bool CanStartNewSession()
    {
        return HasAgents;
    }

    private void RebuildRecentChats(IReadOnlyList<SessionListItem> sessions)
    {
        RecentChats.Clear();
        foreach (var session in sessions)
        {
            RecentChats.Add(new SessionSidebarItem(session.Id, session.Title, session.Preview));
        }
    }

    private void InsertOrUpdateRecentChat(SessionListItem session, bool selectSession)
    {
        var existingIndex = RecentChats
            .Select((item, index) => new { item, index })
            .FirstOrDefault(pair => pair.item.Id == session.Id)
            ?.index;
        var sidebarItem = new SessionSidebarItem(session.Id, session.Title, session.Preview);

        if (existingIndex is int index)
        {
            RecentChats.RemoveAt(index);
        }

        RecentChats.Insert(0, sidebarItem);

        if (selectSession)
        {
            SelectedChat = sidebarItem;
        }
    }

    private void UpdateRecentChatPreview(SessionId sessionId, string preview)
    {
        var existingItem = RecentChats.FirstOrDefault(item => item.Id == sessionId);
        if (existingItem is null)
        {
            return;
        }

        existingItem.Preview = preview;
    }

    private void RebuildTimeline(IReadOnlyList<SessionStreamEntry> entries)
    {
        Messages.Clear();
        _timelineIndexById.Clear();
        foreach (var entry in entries)
        {
            ApplyTimelineEntry(entry);
        }
    }

    private void ClearTimeline()
    {
        Messages.Clear();
        _timelineIndexById.Clear();
    }

    private void ApplyTimelineEntry(SessionStreamEntry entry)
    {
        var timelineItem = MapTimelineItem(entry);
        if (_timelineIndexById.TryGetValue(entry.Id, out var existingIndex))
        {
            Messages[existingIndex] = timelineItem;
            return;
        }

        _timelineIndexById[entry.Id] = Messages.Count;
        Messages.Add(timelineItem);
    }

    private void RebuildAgentPanel(IReadOnlyList<AgentProfileSummary> agents)
    {
        Agents.Clear();
        foreach (var agent in agents)
        {
            Agents.Add(
                new ParticipantItem(
                    agent.Name,
                    $"{agent.ProviderDisplayName} · {agent.ModelName}",
                    GetInitial(agent.Name),
                    ResolveAgentBrush(agent.ProviderKind),
                    agent.Role.ToString(),
                    DesignBrushPalette.BadgeSurfaceBrush));
        }
    }

    private static ChatTimelineItem MapTimelineItem(SessionStreamEntry entry)
    {
        var isCurrentUser = entry.Kind == SessionStreamEntryKind.UserMessage;
        var author = entry.Author;
        var initial = GetInitial(author);
        var avatarBrush = ResolveTimelineBrush(entry);
        var accentLabel = entry.AccentLabel;

        return new ChatTimelineItem(
            entry.Id,
            entry.Kind,
            author,
            entry.Timestamp.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
            entry.Text,
            initial,
            avatarBrush,
            isCurrentUser,
            accentLabel);
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

    private Task RunOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var completionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    action();
                    completionSource.SetResult();
                }
                catch (Exception exception)
                {
                    completionSource.SetException(exception);
                }
            }))
        {
            completionSource.SetException(new InvalidOperationException("Unable to enqueue work to the UI dispatcher."));
        }

        return completionSource.Task;
    }
}
