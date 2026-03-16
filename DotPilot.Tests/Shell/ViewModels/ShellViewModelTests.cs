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
        services.AddSingleton<ShellViewModel>();

        var provider = services.BuildServiceProvider();
        return new TestFixture(provider);
    }

    private sealed class TestFixture(ServiceProvider provider) : IAsyncDisposable
    {
        public ServiceProvider Provider { get; } = provider;

        public ValueTask DisposeAsync()
        {
            return Provider.DisposeAsync();
        }
    }
}
