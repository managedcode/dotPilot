using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Workspace;

[NonParallelizable]
public sealed class StartupWorkspaceHydrationTests
{
    private const int DeleteRetryCount = 20;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(250);

    [Test]
    public async Task EnsureHydratedAsyncWarmsProviderStatusForSubsequentWorkspaceReads()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(StartupWorkspaceHydrationTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var hydration = fixture.Provider.GetRequiredService<IStartupWorkspaceHydration>();

        await hydration.EnsureHydratedAsync(CancellationToken.None);

        commandScope.ReadInvocationCount("codex").Should().Be(1);

        commandScope.WriteCountingVersionCommand("codex", "codex version 2.0.0", delayMilliseconds: 0);
        commandScope.WriteCodexMetadata("gpt-5.1", "gpt-5.1");

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        workspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");
        commandScope.ReadInvocationCount("codex").Should().Be(1);
    }

    [Test]
    public async Task EnsureHydratedAsyncKeepsHydrationRetryableAfterATransientWorkspaceFailure()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            nameof(StartupWorkspaceHydrationTests),
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(rootPath, "dotpilot-agent-sessions.db");

        Directory.CreateDirectory(rootPath);
        Directory.CreateDirectory(databasePath);

        try
        {
            await using var fixture = CreateFixture(new AgentSessionStorageOptions
            {
                DatabasePath = databasePath,
            });
            var hydration = fixture.Provider.GetRequiredService<IStartupWorkspaceHydration>();

            await hydration.EnsureHydratedAsync(CancellationToken.None);

            hydration.IsHydrating.Should().BeFalse();
            hydration.IsReady.Should().BeFalse();

            await DeleteDirectoryWithRetryAsync(databasePath);

            await hydration.EnsureHydratedAsync(CancellationToken.None);

            hydration.IsHydrating.Should().BeFalse();
            hydration.IsReady.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                await DeleteDirectoryWithRetryAsync(rootPath);
            }
        }
    }

    private static TestFixture CreateFixture(AgentSessionStorageOptions? storageOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(storageOptions ?? new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(
            provider,
            provider.GetRequiredService<IAgentWorkspaceState>());
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

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay);
            }
        }
    }
}
