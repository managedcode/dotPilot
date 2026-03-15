using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Workspace;

[NonParallelizable]
public sealed class AgentWorkspaceStateTests
{
    [Test]
    public async Task RepeatedWorkspaceReadsSeeUpdatedProviderStatusWithoutManualRefresh()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentWorkspaceStateTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();

        var initialWorkspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        initialWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");

        commandScope.WriteCountingVersionCommand("codex", "codex version 2.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.1", "gpt-5.1");

        var refreshedWorkspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        refreshedWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(provider, provider.GetRequiredService<IAgentWorkspaceState>());
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentWorkspaceState workspaceState) : IAsyncDisposable
    {
        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
