using DotPilot.Core.ChatSessions;
using DotPilot.Core.AgentBuilder;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.AgentBuilder;

[NonParallelizable]
public sealed class AgentBuilderModelTests
{
    [Test]
    public async Task GenerateDraftAndSaveAgentUsesEnabledProviderModelWhenModelOverrideIsBlank()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.AgentRequest.SetAsync("Create a repository reviewer", CancellationToken.None);
        await model.GenerateAgentDraft(CancellationToken.None);

        var builder = (await model.Builder)!;
        builder.ProviderDisplayName.Should().Be("Codex");
        builder.SuggestedModelName.Should().Be("gpt-5.4");
        builder.CanCreateAgent.Should().BeTrue();
        (await model.ModelName).Should().Be("gpt-5.4");
        (await model.AgentName).Should().Be("Repository Reviewer Agent");

        await model.SaveAgent(CancellationToken.None);

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Agents.Should().Contain(agent =>
            agent.Name == "Repository Reviewer Agent" &&
            agent.ProviderKind == AgentProviderKind.Codex &&
            agent.ModelName == "gpt-5.4");
        workspace.Sessions.Should().Contain(session => session.Title == "Session with Repository Reviewer Agent");
        workspace.SelectedSessionId.Should().NotBeNull();
        fixture.RequestedRoutes.Should().Contain(ShellRoute.Chat);
        (await model.Builder)!.StatusMessage.Should().Contain("ready for local desktop execution");
    }

    [Test]
    public async Task BuilderProjectionReflectsSelectedProviderSuggestionAndVersion()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.HandleSelectedProviderChanged(
            new AgentProviderOption(
                AgentProviderKind.GitHubCopilot,
                "GitHub Copilot",
                "copilot",
                "GitHub Copilot CLI is available on PATH.",
                "gpt-5",
                ["gpt-5", "claude-opus-4.6"],
                "0.0.421",
                false),
            CancellationToken.None);

        var builder = (await model.Builder)!;

        builder.ProviderDisplayName.Should().Be("GitHub Copilot");
        builder.ProviderCommandName.Should().Be("copilot");
        builder.ProviderVersionLabel.Should().Be("Version 0.0.421");
        builder.HasProviderVersion.Should().BeTrue();
        builder.SuggestedModelName.Should().Be("gpt-5");
        builder.SupportedModelNames.Should().ContainInOrder("gpt-5", "claude-opus-4.6");
        builder.HasSupportedModels.Should().BeTrue();
        builder.StatusMessage.Should().Be("GitHub Copilot CLI is available on PATH.");
        builder.CanCreateAgent.Should().BeFalse();
    }

    [Test]
    public async Task BuildManuallyUsesEnabledProviderDefaults()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.BuildManually(CancellationToken.None);

        var surface = await model.Surface;
        surface!.ShowEditor.Should().BeTrue();
        (await model.AgentName).Should().Be("New agent");
        (await model.Builder)!.ProviderDisplayName.Should().Be("Codex");
        (await model.Builder)!.SuggestedModelName.Should().Be("gpt-5.4");
        (await model.OperationMessage).Should().Be("Manual draft ready. Adjust the profile before saving.");
    }

    [Test]
    public async Task BuildManuallyWithoutEnabledRealProviderFallsBackToTheFirstProviderChoice()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.BuildManually(CancellationToken.None);

        var builder = (await model.Builder)!;
        builder.ProviderDisplayName.Should().Be("Codex");
        builder.SuggestedModelName.Should().Be("gpt-5");
        builder.ModelHelperText.Should().Be("Choose one of the supported models for this provider. Suggested: gpt-5.");
        builder.CanCreateAgent.Should().BeFalse();
        (await model.ModelName).Should().Be("gpt-5");
    }

    [Test]
    public async Task HandleSelectedProviderChangedUpdatesModelSuggestionToTheChosenProvider()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        (await model.ModelName).Should().Be("gpt-5");
        await model.SelectedProvider.UpdateAsync(
            _ => new AgentProviderOption(
                AgentProviderKind.Codex,
                "Codex",
                "codex",
                "Codex CLI is ready for local desktop execution.",
                "gpt-5",
                ["gpt-5", "gpt-5.4"],
                "1.0.0",
                true),
            CancellationToken.None);
        await model.SelectedProviderKind.SetAsync(AgentProviderKind.Codex, CancellationToken.None);

        await model.HandleSelectedProviderChanged(
            new AgentProviderOption(
                AgentProviderKind.Codex,
                "Codex",
                "codex",
                "Codex CLI is ready for local desktop execution.",
                "gpt-5.4",
                ["gpt-5.4", "gpt-5"],
                "1.0.0",
                true),
            CancellationToken.None);

        (await model.ModelName).Should().Be("gpt-5.4");
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.Codex);
    }

    [Test]
    public async Task HandleSelectedProviderChangedResetsModelThatIsNotSupportedByTheNextProvider()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.ModelName.SetAsync("gpt-5.4", CancellationToken.None);
        await model.SelectedProvider.UpdateAsync(
            _ => new AgentProviderOption(
                AgentProviderKind.Codex,
                "Codex",
                "codex",
                "Codex CLI is ready for local desktop execution.",
                "gpt-5.4",
                ["gpt-5.4", "gpt-5.2"],
                "1.0.0",
                true),
            CancellationToken.None);
        await model.SelectedProviderKind.SetAsync(AgentProviderKind.Codex, CancellationToken.None);

        await model.HandleSelectedProviderChanged(
            new AgentProviderOption(
                AgentProviderKind.GitHubCopilot,
                "GitHub Copilot",
                "copilot",
                "GitHub Copilot profile authoring is available.",
                "claude-opus-4.6",
                ["claude-opus-4.6", "gpt-5"],
                "1.0.3",
                true),
            CancellationToken.None);

        (await model.ModelName).Should().Be("claude-opus-4.6");
        (await model.SelectedProviderKind).Should().Be(AgentProviderKind.GitHubCopilot);
    }

    [Test]
    public async Task BuilderProjectionUsesSelectedProviderKindWhenThePreviousProviderStateIsStale()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");
        commandScope.WriteVersionCommand("claude", "2.0.75 (Claude Code)");
        commandScope.WriteClaudeSettings("claude-opus-4-6");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.ClaudeCode, true),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        await model.SelectedProvider.UpdateAsync(
            _ => new AgentProviderOption(
                AgentProviderKind.Debug,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                [],
                null,
                false),
            CancellationToken.None);
        await model.SelectedProviderKind.SetAsync(AgentProviderKind.ClaudeCode, CancellationToken.None);

        var builder = await model.Builder;

        builder.Should().NotBeNull();

        builder!.ProviderDisplayName.Should().Be("Claude Code");
        builder.SuggestedModelName.Should().Be("claude-opus-4-6");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.ClaudeCode);
    }

    [Test]
    public async Task BuilderProjectionPrefersTheSelectedProviderWhenTheProviderKindStateIsStale()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");
        commandScope.WriteVersionCommand("claude", "2.0.75 (Claude Code)");
        commandScope.WriteClaudeSettings("claude-opus-4-6");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.ClaudeCode, true),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        await model.SelectedProvider.UpdateAsync(
            _ => new AgentProviderOption(
                AgentProviderKind.ClaudeCode,
                "Claude Code",
                "claude",
                "Claude Code profile authoring is available.",
                "claude-opus-4-6",
                ["claude-opus-4-6", "claude-sonnet-4-5"],
                "2.0.75",
                true),
            CancellationToken.None);
        await model.SelectedProviderKind.SetAsync(AgentProviderKind.Codex, CancellationToken.None);

        var builder = await model.Builder;

        builder.Should().NotBeNull();
        builder!.ProviderDisplayName.Should().Be("Claude Code");
        builder.SuggestedModelName.Should().Be("claude-opus-4-6");
    }

    [Test]
    public async Task BuildManuallyAllowsChoosingASupportedModelBeforeSavingAgent()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.BuildManually(CancellationToken.None);
        await model.AgentName.SetAsync("Codex Custom Model Agent", CancellationToken.None);
        await model.ModelName.SetAsync("gpt-5", CancellationToken.None);
        await model.SystemPrompt.SetAsync("Use the chosen Codex model for this session.", CancellationToken.None);

        await model.SaveAgent(CancellationToken.None);

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Agents.Should().Contain(agent =>
            agent.Name == "Codex Custom Model Agent" &&
            agent.ProviderKind == AgentProviderKind.Codex &&
            agent.ModelName == "gpt-5");
    }

    [Test]
    public async Task StartChatForAgentCreatesAndSelectsSessionForChosenCatalogAgent()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.AgentRequest.SetAsync("Create a repository reviewer", CancellationToken.None);
        await model.GenerateAgentDraft(CancellationToken.None);
        await model.SaveAgent(CancellationToken.None);

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        var createdAgentSummary = workspace.Agents
            .Should()
            .ContainSingle(agent => agent.Name == "Repository Reviewer Agent")
            .Which;
        var createdAgent = new AgentCatalogItem(
            createdAgentSummary.Id,
            "R",
            createdAgentSummary.Name,
            AgentSessionDefaults.CreateAgentDescription(createdAgentSummary.SystemPrompt),
            createdAgentSummary.ProviderDisplayName,
            createdAgentSummary.ModelName,
            false,
            "AgentCatalogStartChatButton_RepositoryReviewerAgent",
            new AgentCatalogStartChatRequest(createdAgentSummary.Id, createdAgentSummary.Name),
            null);

        await model.StartChatForAgent(createdAgent, CancellationToken.None);

        workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Sessions.Should().Contain(session => session.Title == "Session with Repository Reviewer Agent");
        workspace.SelectedSessionId.Should().NotBeNull();
        fixture.RequestedRoutes.Should().Contain(ShellRoute.Chat);
        (await model.OperationMessage).Should().Be("Started a session with Repository Reviewer Agent.");
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
        services.AddSingleton<WorkspaceProjectionNotifier>();
        services.AddSingleton<ShellNavigationNotifier>();

        var provider = services.BuildServiceProvider();
        var workspaceState = provider.GetRequiredService<IAgentWorkspaceState>();
        var navigationNotifier = provider.GetRequiredService<ShellNavigationNotifier>();
        return new TestFixture(provider, workspaceState, navigationNotifier);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ShellNavigationNotifier navigationNotifier;
        private readonly List<ShellRoute> requestedRoutes = [];

        public TestFixture(ServiceProvider provider, IAgentWorkspaceState workspaceState, ShellNavigationNotifier navigationNotifier)
        {
            Provider = provider;
            WorkspaceState = workspaceState;
            this.navigationNotifier = navigationNotifier;
            this.navigationNotifier.Requested += OnNavigationRequested;
        }

        public ServiceProvider Provider { get; }

        public IAgentWorkspaceState WorkspaceState { get; }

        public IReadOnlyList<ShellRoute> RequestedRoutes => requestedRoutes;

        public ValueTask DisposeAsync()
        {
            navigationNotifier.Requested -= OnNavigationRequested;
            return Provider.DisposeAsync();
        }

        private void OnNavigationRequested(object? sender, ShellNavigationRequestedEventArgs e)
        {
            requestedRoutes.Add(e.Route);
        }
    }
}
