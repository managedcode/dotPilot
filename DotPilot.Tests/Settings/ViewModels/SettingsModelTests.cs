using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Settings;

[NonParallelizable]
public sealed class SettingsModelTests
{
    private const int DeleteRetryCount = 10;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(100);

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
    public async Task RequestAndCancelDeleteAllDataUpdatesConfirmationProjection()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);

        await model.RequestDeleteAllData(CancellationToken.None);

        (await model.ShowDeleteAllDataConfirmation).Should().BeTrue();
        (await model.StatusMessage).Should().Be("Confirm deletion to remove all local DotPilot data and reset the app.");

        await model.CancelDeleteAllData(CancellationToken.None);

        (await model.ShowDeleteAllDataConfirmation).Should().BeFalse();
        (await model.StatusMessage).Should().Be("Delete-all-data request cancelled.");
    }

    [Test]
    public async Task ConfirmDeleteAllDataResetsWorkspaceAndPreferences()
    {
        await using var fixture = CreateFixture();
        var model = ActivatorUtilities.CreateInstance<SettingsModel>(fixture.Provider);
        await model.SelectComposerSendBehavior("EnterInsertsNewLine", CancellationToken.None);
        var agent = (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Profile Reset Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Use the debug provider for settings reset coverage.",
                "Profile reset test agent."),
            CancellationToken.None)).ShouldSucceed();
        _ = (await fixture.WorkspaceState.CreateSessionAsync(
            new CreateSessionCommand("Profile reset session", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        File.Exists(fixture.PreferencesFilePath).Should().BeTrue();

        await model.RequestDeleteAllData(CancellationToken.None);
        await model.ConfirmDeleteAllData(CancellationToken.None);

        (await model.ShowDeleteAllDataConfirmation).Should().BeFalse();
        (await model.IsEnterSendsSelected).Should().BeTrue();
        (await model.IsEnterInsertsNewLineSelected).Should().BeFalse();
        (await model.StatusMessage).Should().Be("All local DotPilot data was deleted and the app was reset to defaults.");
        File.Exists(fixture.PreferencesFilePath).Should().BeFalse();

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Sessions.Should().BeEmpty();
        workspace.Agents.Should().ContainSingle(agentSummary =>
            agentSummary.Name == AgentSessionDefaults.SystemAgentName &&
            agentSummary.ProviderKind == AgentProviderKind.Debug);
        workspace.Agents.Should().NotContain(agentSummary => agentSummary.Id == agent.Id);
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
        var tempRoot = CreateTempRootDirectory();
        var preferencesFilePath = Path.Combine(tempRoot, "preferences", "operator-preferences.json");
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceProjectionNotifier>();
        services.AddSingleton(new OperatorPreferencesStorageOptions
        {
            FilePath = preferencesFilePath,
        });
        services.AddSingleton<IOperatorPreferencesStore, LocalOperatorPreferencesStore>();
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        var workspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        return new TestFixture(provider, workspaceState, tempRoot, preferencesFilePath);
    }

    private static string CreateTempRootDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotpilot-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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

    private sealed class TestFixture(
        ServiceProvider provider,
        IAgentWorkspaceState workspaceState,
        string tempRootPath,
        string preferencesFilePath) : IAsyncDisposable
    {
        private readonly string _tempRootPath = tempRootPath;

        public ServiceProvider Provider { get; } = provider;

        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public string PreferencesFilePath { get; } = preferencesFilePath;

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            await DeleteDirectoryWithRetryAsync(_tempRootPath);
        }
    }
}
