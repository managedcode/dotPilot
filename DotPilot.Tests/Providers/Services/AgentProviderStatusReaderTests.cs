using DotPilot.Core.ChatSessions;
using DotPilot.Core.Providers.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Providers;

[NonParallelizable]
public sealed class AgentProviderStatusReaderTests
{
    [Test]
    public async Task RefreshWorkspaceAsyncReadsProviderStatusFromCurrentSourceOfTruth()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5", "gpt-5-mini");

        await using var fixture = CreateFixture();
        _ = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var initialWorkspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        initialWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");

        commandScope.WriteVersionCommand("codex", "codex version 2.0.0");

        var workspace = (await fixture.WorkspaceState.RefreshWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");
    }

    [Test]
    public async Task EnabledCodexProviderReportsReadyRuntimeAndCliMetadata()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5", "gpt-5-mini");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Ready);
        provider.StatusSummary.Should().Contain("ready for local desktop execution");
        provider.SuggestedModelName.Should().Be("gpt-5.4");
        provider.InstalledVersion.Should().Be("1.0.0");
        provider.Details.Should().Contain(detail => detail.Label == "Default model" && detail.Value == "gpt-5.4");
        provider.Details.Should().Contain(detail => detail.Label == "Available models" && detail.Value.Contains("gpt-5-mini", StringComparison.Ordinal));
    }

    [Test]
    public async Task EnabledExternalProviderWithoutLiveRuntimeStillAllowsProfileAuthoring()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteVersionCommand("copilot", "copilot version 0.0.421");

        await using var fixture = CreateFixture();
        var provider = (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.GitHubCopilot, true),
            CancellationToken.None)).ShouldSucceed();

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeTrue();
        provider.Status.Should().Be(AgentProviderStatus.Unsupported);
        provider.StatusSummary.Should().Contain("profile authoring is available");
        provider.InstalledVersion.Should().Be("0.0.421");
    }

    [Test]
    public async Task ConcurrentReadsShareOneInFlightProviderProbe()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentProviderStatusReaderTests));
        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");

        await using var fixture = CreateFixture();
        var reader = fixture.Provider.GetRequiredService<IAgentProviderStatusReader>();

        await Task.WhenAll(
            reader.ReadAsync(CancellationToken.None).AsTask(),
            reader.ReadAsync(CancellationToken.None).AsTask(),
            reader.ReadAsync(CancellationToken.None).AsTask());

        commandScope.ReadInvocationCount("codex").Should().Be(1);
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
        return new TestFixture(
            provider,
            provider.GetRequiredService<IAgentSessionService>());
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;

        public TestFixture(ServiceProvider provider, IAgentSessionService service)
        {
            this.provider = provider;
            Provider = provider;
            Service = service;
            WorkspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        }

        public ServiceProvider Provider { get; }

        public IAgentSessionService Service { get; }

        public IAgentWorkspaceState WorkspaceState { get; }

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
