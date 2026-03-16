using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Chat;

public sealed class ChatModelTests
{
    [Test]
    public async Task StartNewSessionUsesSeededDefaultSystemAgentWhenNoCustomAgentExists()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        await model.StartNewSession(CancellationToken.None);

        var activeSession = await model.ActiveSession;
        activeSession.Should().NotBeNull();
        activeSession!.Title.Should().Be($"Session with {AgentSessionDefaults.SystemAgentName}");
        activeSession.StatusSummary.Should().Contain("Debug Provider");
    }

    [Test]
    public async Task SendMessageStreamsDebugTranscriptForAnActiveSession()
    {
        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Debug Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Be deterministic for automated verification."),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        await model.StartNewSession(CancellationToken.None);
        await model.ComposerText.SetAsync("hello from model", CancellationToken.None);

        await model.SendMessage(CancellationToken.None);

        var activeSession = await model.ActiveSession;
        activeSession.Should().NotBeNull();
        activeSession!.Messages.Should().Contain(message =>
            message.Content.Contains("Debug provider received: hello from model", StringComparison.Ordinal));
        activeSession.Messages.Should().Contain(message =>
            message.Content.Contains("Debug workflow finished", StringComparison.Ordinal));
        activeSession.Messages.Should().Contain(message =>
            message.Kind == SessionStreamEntryKind.ToolStarted &&
            string.Equals(message.AccentLabel, "tool", StringComparison.Ordinal) &&
            message.Content.Contains("Preparing local debug workflow", StringComparison.Ordinal));
        activeSession.Messages.Should().Contain(message =>
            message.Kind == SessionStreamEntryKind.ToolCompleted &&
            string.Equals(message.AccentLabel, "tool", StringComparison.Ordinal) &&
            message.Content.Contains("Debug workflow finished", StringComparison.Ordinal));
        activeSession.Messages.Should().Contain(message =>
            message.Kind == SessionStreamEntryKind.Status &&
            string.Equals(message.AccentLabel, "status", StringComparison.Ordinal) &&
            message.Content.Contains("Running Debug Agent with Debug Provider", StringComparison.Ordinal));
        activeSession.StatusSummary.Should().Be("Debug Agent · Debug Provider");
    }

    [Test]
    public async Task StartNewSessionUsesNewestCustomAgentWhenCustomNameSortsAfterSystemAgent()
    {
        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Repository Reviewer Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Review repository changes and explain the diff."),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        await model.StartNewSession(CancellationToken.None);

        var activeSession = await model.ActiveSession;
        activeSession.Should().NotBeNull();
        activeSession!.Title.Should().Be("Session with Repository Reviewer Agent");
        activeSession.StatusSummary.Should().Be("Repository Reviewer Agent · Debug Provider");
    }

    [Test]
    public async Task RefreshIgnoresCancellationDuringWorkspaceProbe()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(ChatModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        _ = await model.RecentChats;

        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await model.Refresh(cancellationSource.Token);

        (await model.FeedbackMessage).Should().BeEmpty();
    }

    [Test]
    public async Task FleetBoardShowsTheActiveSessionWhileStreamingAndClearsAfterCompletion()
    {
        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Fleet Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic for fleet board verification."),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        await model.StartNewSession(CancellationToken.None);
        var selectedChat = await model.SelectedChat;

        await using var enumerator = fixture.WorkspaceState.SendMessageAsync(
                new SendSessionMessageCommand(selectedChat!.Id, "fleet activity"),
                CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var observedLiveBoard = false;
        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
            var board = await model.FleetBoard;
            board.Should().NotBeNull();
            if (board!.ActiveSessions.Count == 0)
            {
                continue;
            }

            observedLiveBoard = true;
            board.Metrics.Should().Contain(metric =>
                metric.Label == "Live sessions" &&
                metric.Value == "1");
            board.ActiveSessions.Should().Contain(item =>
                item.Title == selectedChat.Title &&
                item.Summary.Contains("Fleet Agent", StringComparison.Ordinal));
            break;
        }

        observedLiveBoard.Should().BeTrue();

        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
        }

        FleetBoardView? completedBoard = null;
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            completedBoard = await model.FleetBoard;
            completedBoard.Should().NotBeNull();
            if (completedBoard!.ActiveSessions.Count == 0)
            {
                break;
            }

            await Task.Delay(50);
        }

        completedBoard.Should().NotBeNull();
        completedBoard!.ActiveSessions.Should().BeEmpty();
        completedBoard.ShowActiveSessionsEmptyState.Should().BeTrue();
    }

    [Test]
    public async Task FleetBoardReusesTheWarmProviderSnapshotDuringLiveStreaming()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(ChatModelTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Fleet Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic for fleet board verification."),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        var initialBoard = await model.FleetBoard;
        initialBoard.Should().NotBeNull();
        var warmInvocationCount = commandScope.ReadInvocationCount("codex");

        await model.StartNewSession(CancellationToken.None);
        var postStartInvocationCount = commandScope.ReadInvocationCount("codex");
        postStartInvocationCount.Should().BeGreaterThanOrEqualTo(warmInvocationCount);

        var selectedChat = await model.SelectedChat;

        await using var enumerator = fixture.WorkspaceState.SendMessageAsync(
                new SendSessionMessageCommand(selectedChat!.Id, "fleet activity"),
                CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var observedLiveBoard = false;
        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
            var board = await model.FleetBoard;
            board.Should().NotBeNull();
            if (board!.ActiveSessions.Count == 0)
            {
                continue;
            }

            observedLiveBoard = true;
            break;
        }

        observedLiveBoard.Should().BeTrue();
        commandScope.ReadInvocationCount("codex").Should().Be(postStartInvocationCount);

        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
        }
    }

    [Test]
    public async Task OpenFleetSessionSelectsTheRequestedActiveSession()
    {
        await using var fixture = await CreateFixtureAsync();
        var agent = (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Navigator Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic for fleet navigation verification."),
            CancellationToken.None)).ShouldSucceed();
        var firstSession = (await fixture.WorkspaceState.CreateSessionAsync(
            new CreateSessionCommand("Fleet Session One", agent.Id),
            CancellationToken.None)).ShouldSucceed();
        var secondSession = (await fixture.WorkspaceState.CreateSessionAsync(
            new CreateSessionCommand("Fleet Session Two", agent.Id),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);
        await model.SelectedChat.UpdateAsync(
            _ => new SessionSidebarItem(secondSession.Session.Id, secondSession.Session.Title, secondSession.Session.Preview),
            CancellationToken.None);

        await using var enumerator = fixture.WorkspaceState.SendMessageAsync(
                new SendSessionMessageCommand(firstSession.Session.Id, "jump back to this"),
                CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        FleetBoardSessionItem? activeSession = null;
        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
            var board = await model.FleetBoard;
            board.Should().NotBeNull();
            activeSession = board!.ActiveSessions.FirstOrDefault(item =>
                item.Title == firstSession.Session.Title);
            if (activeSession is not null)
            {
                break;
            }
        }

        activeSession.Should().NotBeNull();
        await model.OpenFleetSession(activeSession!.OpenRequest, CancellationToken.None);

        var selectedChat = await model.SelectedChat;
        selectedChat.Should().NotBeNull();
        selectedChat!.Id.Should().Be(firstSession.Session.Id);
        selectedChat.Title.Should().Be(firstSession.Session.Title);

        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
        }
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceProjectionNotifier>();
        services.AddSingleton<UiDispatcher>();
        services.AddSingleton<IOperatorPreferencesStore, LocalOperatorPreferencesStore>();
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        var workspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        return new TestFixture(provider, workspaceState);
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentWorkspaceState workspaceState) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }
}
