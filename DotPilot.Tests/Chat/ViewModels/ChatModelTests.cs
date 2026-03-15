using DotPilot.Core.ControlPlaneDomain;
using DotPilot.Presentation;
using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
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
                AgentRoleKind.Operator,
                AgentProviderKind.Debug,
                "debug-echo",
                "Be deterministic for automated verification.",
                ["Shell"]),
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
        activeSession.StatusSummary.Should().Be("Debug Agent · Debug Provider");
    }

    [Test]
    public async Task StartNewSessionUsesNewestCustomAgentWhenCustomNameSortsAfterSystemAgent()
    {
        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Repository Reviewer Agent",
                AgentRoleKind.Reviewer,
                AgentProviderKind.Debug,
                "debug-echo",
                "Review repository changes and explain the diff.",
                ["Git", "Files"]),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<ChatModel>(fixture.Provider);

        await model.StartNewSession(CancellationToken.None);

        var activeSession = await model.ActiveSession;
        activeSession.Should().NotBeNull();
        activeSession!.Title.Should().Be("Session with Repository Reviewer Agent");
        activeSession.StatusSummary.Should().Be("Repository Reviewer Agent · Debug Provider");
    }

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceProjectionNotifier>();
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
