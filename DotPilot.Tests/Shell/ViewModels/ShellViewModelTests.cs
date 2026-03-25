using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace DotPilot.Tests.Shell.ViewModels;

[NonParallelizable]
public sealed class ShellViewModelTests
{
    [Test]
    public async Task StartupOverlayRemainsVisibleUntilWorkspaceHydrationCompletes()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(ShellViewModelTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var viewModel = fixture.Provider.GetRequiredService<ShellViewModel>();
        var hydration = fixture.Provider.GetRequiredService<IStartupWorkspaceHydration>();

        viewModel.StartupOverlayVisibility.Should().Be(Visibility.Visible);

        var hydrationTask = hydration.EnsureHydratedAsync(CancellationToken.None).AsTask();

        viewModel.StartupOverlayVisibility.Should().Be(Visibility.Visible);

        await hydrationTask;

        viewModel.StartupOverlayVisibility.Should().Be(Visibility.Collapsed);
    }

    [Test]
    public async Task StartupOverlayCollapsesAfterTheInitialHydrationAttemptFails()
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            nameof(ShellViewModelTests),
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
            var viewModel = fixture.Provider.GetRequiredService<ShellViewModel>();
            var hydration = fixture.Provider.GetRequiredService<IStartupWorkspaceHydration>();

            viewModel.StartupOverlayVisibility.Should().Be(Visibility.Visible);

            await hydration.EnsureHydratedAsync(CancellationToken.None);

            hydration.IsReady.Should().BeFalse();
            hydration.HasCompletedInitialAttempt.Should().BeTrue();
            viewModel.StartupOverlayVisibility.Should().Be(Visibility.Collapsed);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Test]
    public async Task LiveSessionIndicatorAppearsWhileStreamingAndCollapsesAfterCompletion()
    {
        await using var fixture = CreateFixture();
        var viewModel = fixture.Provider.GetRequiredService<ShellViewModel>();

        var agent = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Sleep Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic while testing the shell indicator.",
                "Shell indicator test agent."),
            CancellationToken.None)).ShouldSucceed();
        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Session with Sleep Agent", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        await using var enumerator = fixture.Service.SendMessageAsync(
                new SendSessionMessageCommand(session.Session.Id, "hello from shell test"),
                CancellationToken.None)
            .GetAsyncEnumerator(CancellationToken.None);

        var observedIndicator = false;
        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
            if (viewModel.LiveSessionIndicatorVisibility != Visibility.Visible)
            {
                continue;
            }

            observedIndicator = true;
            viewModel.LiveSessionIndicatorTitle.Should().Be("Live session active");
            viewModel.LiveSessionIndicatorSummary.Should().Contain("Sleep Agent");
            viewModel.LiveSessionIndicatorSummary.Should().Contain("Session with Sleep Agent");
            break;
        }

        observedIndicator.Should().BeTrue();

        while (await enumerator.MoveNextAsync())
        {
            _ = enumerator.Current.ShouldSucceed();
        }

        viewModel.LiveSessionIndicatorVisibility.Should().Be(Visibility.Collapsed);
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
        services.AddSingleton<UiDispatcher>();
        services.AddSingleton<DesktopSleepPreventionService>();
        services.AddSingleton<ShellViewModel>();

        var provider = services.BuildServiceProvider();
        return new TestFixture(provider);
    }

    private sealed class TestFixture(ServiceProvider provider) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public IAgentSessionService Service { get; } = provider.GetRequiredService<IAgentSessionService>();

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }
}
