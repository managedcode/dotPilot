using DotPilot.Presentation;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.AgentBuilder;

public sealed class SecondModelTests
{
    [Test]
    public async Task GenerateDraftAndSaveAgentUsesSuggestedDebugModelWhenModelOverrideIsBlank()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.AgentRequest.SetAsync("Create a repository reviewer", CancellationToken.None);
        await model.GenerateAgentDraft(CancellationToken.None);

        var builder = (await model.Builder)!;
        builder.ProviderDisplayName.Should().Be("Debug Provider");
        builder.SuggestedModelName.Should().Be("debug-echo");
        builder.CanCreateAgent.Should().BeTrue();
        (await model.ModelName).Should().Be("debug-echo");
        (await model.AgentName).Should().Be("Repository Reviewer Agent");

        await model.SaveAgent(CancellationToken.None);

        var workspace = await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None);
        workspace.Agents.Should().Contain(agent =>
            agent.Name == "Repository Reviewer Agent" &&
            agent.ProviderKind == AgentProviderKind.Debug &&
            agent.ModelName == "debug-echo");
        (await model.OperationMessage).Should().Be("Saved Repository Reviewer Agent using Debug Provider.");
        (await model.Builder)!.StatusMessage.Should().Be("Built in and ready for deterministic local testing.");
    }

    [Test]
    public async Task BuilderProjectionReflectsSelectedProviderSuggestionAndVersion()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        await model.SelectedProvider.UpdateAsync(
            _ => new AgentProviderOption(
                AgentProviderKind.GitHubCopilot,
                "GitHub Copilot",
                "copilot",
                "GitHub Copilot CLI is available on PATH.",
                "0.0.421",
                false),
            CancellationToken.None);

        var builder = (await model.Builder)!;

        builder.ProviderDisplayName.Should().Be("GitHub Copilot");
        builder.ProviderCommandName.Should().Be("copilot");
        builder.ProviderVersionLabel.Should().Be("Version 0.0.421");
        builder.HasProviderVersion.Should().BeTrue();
        builder.SuggestedModelName.Should().Be("gpt-5");
        builder.StatusMessage.Should().Be("GitHub Copilot CLI is available on PATH.");
        builder.CanCreateAgent.Should().BeFalse();
    }

    [Test]
    public async Task BuildManuallyOpensEditorWithDefaultDraft()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.BuildManually(CancellationToken.None);

        var surface = await model.Surface;
        surface!.ShowEditor.Should().BeTrue();
        (await model.AgentName).Should().Be("New agent");
        (await model.Builder)!.SuggestedModelName.Should().Be("debug-echo");
        (await model.OperationMessage).Should().Be("Manual draft ready. Adjust the profile before saving.");
    }

    [Test]
    public async Task HandleSelectedProviderChangedKeepsRunnableModelWhenProviderCannotCreateAgents()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        await model.BuildManually(CancellationToken.None);
        (await model.ModelName).Should().Be("debug-echo");

        await model.HandleSelectedProviderChanged(
            new AgentProviderOption(
                AgentProviderKind.Codex,
                "Codex",
                "codex",
                "Codex CLI is detected, but live desktop execution is not available in this app yet.",
                "1.0.0",
                false),
            CancellationToken.None);

        (await model.ModelName).Should().Be("debug-echo");
        (await model.SelectedProvider)!.Kind.Should().Be(AgentProviderKind.Codex);
    }

    [Test]
    public async Task StartChatForAgentCreatesAndSelectsSessionForChosenCatalogAgent()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        await model.OpenCreateAgent(CancellationToken.None);
        await model.AgentRequest.SetAsync("Create a repository reviewer", CancellationToken.None);
        await model.GenerateAgentDraft(CancellationToken.None);
        await model.SaveAgent(CancellationToken.None);

        var createdAgent = (await model.Agents)
            .Should()
            .ContainSingle(agent => agent.Name == "Repository Reviewer Agent")
            .Which;

        await model.StartChatForAgent(createdAgent, CancellationToken.None);

        var workspace = await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None);
        workspace.Sessions.Should().Contain(session => session.Title == "Session with Repository Reviewer Agent");
        workspace.SelectedSessionId.Should().NotBeNull();
        (await model.OperationMessage).Should().Be("Started a session with Repository Reviewer Agent. Switch to Chat to continue.");
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
