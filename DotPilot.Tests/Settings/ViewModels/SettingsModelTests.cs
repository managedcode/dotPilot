using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Settings;

[NonParallelizable]
public sealed class SettingsModelTests
{
    [Test]
    public async Task ProvidersExposeOnlyTheThreeRealConsoleProvidersAndDefaultToCodex()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        providers.Select(provider => provider.Kind).Should().ContainInOrder(
            AgentProviderKind.Codex,
            AgentProviderKind.ClaudeCode,
            AgentProviderKind.GitHubCopilot);
        providers.Should().OnlyContain(provider => provider.Kind != AgentProviderKind.Debug);
        (await model.SelectedProviderTitle).Should().Be("Codex");
        (await model.ToggleActionLabel).Should().Be("Enable provider");
        (await model.CanToggleSelectedProvider).Should().BeTrue();
    }

    [Test]
    public async Task ToggleSelectedProviderUpdatesProjectionToTheSelectedRealProvider()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(SettingsModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);
        _ = await model.Providers;

        await model.ToggleSelectedProvider(CancellationToken.None);

        (await model.SelectedProviderTitle).Should().Be("Codex");
        (await model.ToggleActionLabel).Should().Be("Disable provider");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.IsEnabled.Should().BeTrue();

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Codex &&
            provider.IsEnabled &&
            provider.CanCreateAgents);
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            provider.IsEnabled);
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
    public async Task SelectProviderSurfacesCopilotSuggestedAndSupportedModels()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(SettingsModelTests));
        commandScope.WriteVersionCommand("copilot", "copilot version 1.0.3");
        commandScope.WriteCopilotConfig("claude-opus-4.6");
        await using var fixture = CreateFixture();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.GitHubCopilot, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        var selectedProvider = providers.First(provider => provider.Kind == AgentProviderKind.GitHubCopilot);

        await model.SelectProvider(selectedProvider, CancellationToken.None);

        var details = await model.SelectedProviderDetails;
        details.Should().Contain(detail => detail.Label == "Installed version" && detail.Value == "1.0.3");
        details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "claude-opus-4.6");
        details.Should().Contain(detail => detail.Label == "Supported models" && detail.Value == "claude-opus-4.6");
    }

    [Test]
    public async Task SelectProviderSurfacesClaudeSuggestedAndSupportedModels()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(SettingsModelTests));
        commandScope.WriteVersionCommand("claude", "claude version 2.0.75");
        commandScope.WriteClaudeSettings("claude-opus-4-6");
        await using var fixture = CreateFixture();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.ClaudeCode, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        var providers = await model.Providers;
        var selectedProvider = providers.First(provider => provider.Kind == AgentProviderKind.ClaudeCode);

        await model.SelectProvider(selectedProvider, CancellationToken.None);

        var details = await model.SelectedProviderDetails;
        details.Should().Contain(detail => detail.Label == "Installed version" && detail.Value == "2.0.75");
        details.Should().Contain(detail => detail.Label == "Suggested model" && detail.Value == "claude-opus-4-6");
        details.Should().Contain(detail =>
            detail.Label == "Supported models" &&
            detail.Value.Contains("claude-opus-4-6", StringComparison.Ordinal));
    }

    [Test]
    public async Task SelectComposerSendBehaviorUpdatesProjectionAndPreferenceStore()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        (await model.IsEnterSendsSelected).Should().BeTrue();
        (await model.IsEnterInsertsNewLineSelected).Should().BeFalse();

        await model.SelectComposerSendBehavior("EnterInsertsNewLine", CancellationToken.None);

        (await model.IsEnterSendsSelected).Should().BeFalse();
        (await model.IsEnterInsertsNewLineSelected).Should().BeTrue();
        (await model.ComposerSendBehaviorHint).Should().Be("Enter adds a new line. Enter with a modifier sends.");

        var preferencesStore = fixture.Provider.GetRequiredService<IOperatorPreferencesStore>();
        (await preferencesStore.GetAsync(CancellationToken.None)).ComposerSendBehavior
            .Should().Be(ComposerSendBehavior.EnterInsertsNewLine);
    }

    [Test]
    public async Task RefreshIgnoresCancellationDuringProviderProbe()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(SettingsModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        _ = await model.Providers;

        commandScope.WriteCountingVersionCommand("codex", "codex version 1.0.0", delayMilliseconds: 300);
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await model.Refresh(cancellationSource.Token);

        (await model.StatusMessage).Should().BeOneOf(string.Empty, "Provider readiness refreshed.");
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceProjectionNotifier>();
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
