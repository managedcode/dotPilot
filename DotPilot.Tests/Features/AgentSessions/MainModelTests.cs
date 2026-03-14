using DotPilot.Core.Features.AgentSessions;
using DotPilot.Presentation;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

public sealed class MainModelTests
{
    [Test]
    public async Task SendMessageStreamsDebugTranscriptForAnActiveSession()
    {
        await using var fixture = await CreateFixtureAsync();
        await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Debug Agent",
                AgentRoleKind.Operator,
                AgentProviderKind.Debug,
                "debug-echo",
                "Be deterministic for automated verification.",
                ["Shell"]),
            CancellationToken.None);
        var model = ActivatorUtilities.CreateInstance<MainModel>(fixture.Provider);

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

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        var workspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        await workspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None);
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
