using DotPilot.Core.ChatSessions;
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

        (await model.BuilderProviderDisplayName).Should().Be("Codex");
        (await model.BuilderSuggestedModelName).Should().Be("gpt-5.4");
        (await model.BuilderCanCreateAgent).Should().BeTrue();
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
        (await model.BuilderStatusMessage).Should().Contain("ready for local desktop execution");
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

        (await model.BuilderProviderDisplayName).Should().Be("GitHub Copilot");
        (await model.BuilderProviderCommandName).Should().Be("copilot");
        (await model.BuilderProviderVersionLabel).Should().Be("Version 0.0.421");
        (await model.BuilderHasProviderVersion).Should().BeTrue();
        (await model.BuilderSuggestedModelName).Should().Be("gpt-5");
        (await model.BuilderSupportedModelNames).Should().ContainInOrder("gpt-5", "claude-opus-4.6");
        (await model.BuilderHasSupportedModels).Should().BeTrue();
        (await model.BuilderStatusMessage).Should().Be("GitHub Copilot CLI is available on PATH.");
        (await model.BuilderCanCreateAgent).Should().BeFalse();
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
        (await model.BuilderProviderDisplayName).Should().Be("Codex");
        (await model.BuilderSuggestedModelName).Should().Be("gpt-5.4");
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

        (await model.BuilderProviderDisplayName).Should().Be("Codex");
        (await model.BuilderSuggestedModelName).Should().BeEmpty();
        (await model.BuilderModelHelperText).Should().Be("Select an enabled provider to load its supported models.");
        (await model.BuilderHasSupportedModels).Should().BeFalse();
        (await model.BuilderCanCreateAgent).Should().BeFalse();
        (await model.ModelName).Should().BeNullOrEmpty();
    }

    [Test]
    public async Task HandleSelectedProviderChangedUpdatesModelSuggestionToTheChosenProvider()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        (await model.ModelName).Should().BeNullOrEmpty();
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
                "GitHub Copilot CLI is ready for local desktop execution.",
                "claude-opus-4.6",
                ["claude-opus-4.6", "gpt-5"],
                "1.0.3",
                true),
            CancellationToken.None);

        (await model.ModelName).Should().Be("claude-opus-4.6");
        (await model.SelectedProviderKind).Should().Be(AgentProviderKind.GitHubCopilot);
    }

    [Test]
    public async Task HandleProviderSelectionChangedUsesProviderKindParameterWhenNoProviderOptionIsProvided()
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
        await model.HandleProviderSelectionChanged(AgentProviderKind.ClaudeCode, CancellationToken.None);

        (await model.BuilderProviderDisplayName).Should().Be("Claude Code");
        (await model.BuilderSuggestedModelName).Should().Be("claude-opus-4-6");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.ClaudeCode);
    }

    [Test]
    public async Task HandleProviderSelectionChangedUsesGeminiProviderKindParameterWhenNoProviderOptionIsProvided()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("gemini", "gemini-cli 0.34.0");
        commandScope.WriteGeminiMetadata("gemini-2.5-pro", "gemini-2.5-pro", "gemini-2.5-flash");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Gemini, true),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        await model.HandleProviderSelectionChanged(AgentProviderKind.Gemini, CancellationToken.None);

        (await model.BuilderProviderDisplayName).Should().Be("Gemini");
        (await model.BuilderSuggestedModelName).Should().Be("gemini-2.5-pro");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.Gemini);
    }

    [Test]
    public async Task HandleProviderSelectionChangedUsesOnnxProviderKindParameterWhenNoProviderOptionIsProvided()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteOnnxModelDirectory("granite-vision-onnx");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Onnx, true),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        await model.HandleProviderSelectionChanged(AgentProviderKind.Onnx, CancellationToken.None);

        (await model.BuilderProviderDisplayName).Should().Be("ONNX Runtime GenAI");
        (await model.BuilderSuggestedModelName).Should().Be("granite-vision-onnx");
        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.Onnx);
    }

    [Test]
    public async Task HandleProviderSelectionChangedShowsEveryAddedOnnxModelInTheDropdown()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        var firstModelPath = commandScope.WriteOnnxModelDirectory("granite-vision-onnx", modelType: "granite");
        var secondModelPath = commandScope.WriteOnnxModelDirectory("qwen3-text-onnx", modelType: "qwen3");

        await using var fixture = await CreateFixtureAsync();
        _ = (await fixture.WorkspaceState.SetLocalModelPathAsync(
            new SetLocalModelPathCommand(AgentProviderKind.Onnx, firstModelPath),
            CancellationToken.None)).ShouldSucceed();
        _ = (await fixture.WorkspaceState.SetLocalModelPathAsync(
            new SetLocalModelPathCommand(AgentProviderKind.Onnx, secondModelPath),
            CancellationToken.None)).ShouldSucceed();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Onnx, true),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        await model.HandleProviderSelectionChanged(AgentProviderKind.Onnx, CancellationToken.None);

        (await model.BuilderSuggestedModelName).Should().Be("qwen3-text-onnx");
        (await model.BuilderSupportedModelNames).Should().ContainInOrder("qwen3-text-onnx", "granite-vision-onnx");
        (await model.ModelName).Should().Be("qwen3-text-onnx");
    }

    [Test]
    public async Task HandleProviderSelectionChangedUsesTheProvidedProviderWhenThePreviousProviderRemainsPopulated()
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
        (await model.BuilderProviderDisplayName).Should().Be("Codex");

        await model.HandleProviderSelectionChanged(
            new AgentProviderOption(
                AgentProviderKind.ClaudeCode,
                "Claude Code",
                "claude",
                "Claude Code CLI is ready for local desktop execution.",
                "claude-opus-4-6",
                ["claude-opus-4-6", "claude-sonnet-4-5"],
                "2.0.75",
                true),
            CancellationToken.None);

        (await model.SelectedProvider).Should().NotBeNull();
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.ClaudeCode);
        (await model.SelectedProviderKind).Should().Be(AgentProviderKind.ClaudeCode);
        (await model.ModelName).Should().Be("claude-opus-4-6");

        (await model.BuilderProviderDisplayName).Should().Be("Claude Code");
        (await model.BuilderSuggestedModelName).Should().Be("claude-opus-4-6");
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
    public async Task OpenEditAgentLoadsAnExistingProfileAndSaveUpdatesItInPlace()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentBuilderModelTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = await CreateFixtureAsync();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        var existing = (await fixture.WorkspaceState.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Editable Codex Agent",
                AgentProviderKind.Codex,
                "gpt-5.4",
                "Answer clearly.",
                "Editable Codex profile."),
            CancellationToken.None)).ShouldSucceed();

        var model = ActivatorUtilities.CreateInstance<AgentBuilderModel>(fixture.Provider);

        await model.OpenEditAgent(existing.Id, CancellationToken.None);

        (await model.Surface).Should().NotBeNull();
        (await model.Surface)!.Title.Should().Be("Edit agent");
        (await model.Surface)!.PrimaryActionLabel.Should().Be("Save changes");
        (await model.AgentName).Should().Be("Editable Codex Agent");
        (await model.AgentDescription).Should().Be("Editable Codex profile.");
        (await model.ModelName).Should().Be("gpt-5.4");

        await model.AgentName.SetAsync("Edited Codex Agent", CancellationToken.None);
        await model.AgentDescription.SetAsync("Edited Codex profile.", CancellationToken.None);
        await model.SystemPrompt.SetAsync("Answer even more clearly.", CancellationToken.None);

        await model.SaveAgent(CancellationToken.None);

        var workspace = (await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        workspace.Agents.Should().ContainSingle(agent =>
            agent.Id == existing.Id &&
            agent.Name == "Edited Codex Agent" &&
            agent.Description == "Edited Codex profile." &&
            agent.SystemPrompt == "Answer even more clearly.");
        workspace.Sessions.Should().BeEmpty();
        fixture.RequestedRoutes.Should().BeEmpty();
        (await model.Surface)!.ShowCatalog.Should().BeTrue();
        (await model.OperationMessage).Should().Be("Saved changes to Edited Codex Agent using Codex.");
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
            createdAgentSummary.Description,
            createdAgentSummary.ProviderDisplayName,
            createdAgentSummary.ModelName,
            false,
            "AgentCatalogEditButton_RepositoryReviewerAgent",
            new AgentCatalogEditRequest(createdAgentSummary.Id, createdAgentSummary.Name),
            null,
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
        services.AddSingleton<SessionSelectionNotifier>();

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
