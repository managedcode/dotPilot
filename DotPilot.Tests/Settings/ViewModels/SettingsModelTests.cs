using DotPilot.Presentation;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Settings;

public sealed class SettingsModelTests
{
    [Test]
    public async Task ToggleSelectedProviderUpdatesProjectionToEnabledDebugProvider()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        providers.Should().ContainSingle(provider => provider.Kind == AgentProviderKind.Debug);
        (await model.SelectedProviderTitle).Should().Be("Debug Provider");
        (await model.ToggleActionLabel).Should().Be("Disable provider");
        (await model.CanToggleSelectedProvider).Should().BeTrue();

        await model.ToggleSelectedProvider(CancellationToken.None);

        (await model.SelectedProviderTitle).Should().Be("Debug Provider");
        (await model.ToggleActionLabel).Should().Be("Enable provider");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.IsEnabled.Should().BeFalse();

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            !provider.IsEnabled &&
            !provider.CanCreateAgents);
    }

    [Test]
    public async Task SelectProviderUpdatesProjectionToChosenProvider()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        var selectedProvider = providers.First(provider => provider.Kind == AgentProviderKind.Codex);

        await model.SelectProvider(selectedProvider, CancellationToken.None);

        (await model.SelectedProviderTitle).Should().Be(selectedProvider.DisplayName);
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.Codex);
    }

    [Test]
    public async Task SelectComposerSendBehaviorUpdatesProjectionAndWorkspacePreference()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        (await model.IsEnterSendsSelected).Should().BeTrue();
        (await model.IsEnterInsertsNewLineSelected).Should().BeFalse();

        await model.SelectComposerSendBehavior("EnterInsertsNewLine", CancellationToken.None);

        (await model.IsEnterSendsSelected).Should().BeFalse();
        (await model.IsEnterInsertsNewLineSelected).Should().BeTrue();
        (await model.ComposerSendBehaviorHint).Should().Be("Enter adds a new line. Enter with a modifier sends.");

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Preferences.ComposerSendBehavior.Should().Be(ComposerSendBehavior.EnterInsertsNewLine);
    }

    private static TestFixture CreateFixture()
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
