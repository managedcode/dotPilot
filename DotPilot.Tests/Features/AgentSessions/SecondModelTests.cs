using DotPilot.Core.Features.AgentSessions;
using DotPilot.Presentation;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

public sealed class SecondModelTests
{
    [Test]
    public async Task CreateAgentUsesSuggestedDebugModelWhenModelOverrideIsBlank()
    {
        await using var fixture = await CreateFixtureAsync();
        var model = ActivatorUtilities.CreateInstance<SecondModel>(fixture.Provider);

        var builder = (await model.Builder)!;
        builder.ProviderDisplayName.Should().Be("Debug Provider");
        builder.SuggestedModelName.Should().Be("debug-echo");
        builder.CanCreateAgent.Should().BeTrue();
        (await model.ModelName).Should().BeEmpty();

        await model.CreateAgent(CancellationToken.None);

        var workspace = await fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None);
        workspace.Agents.Should().ContainSingle(agent =>
            agent.Name == "Debug Agent" &&
            agent.ProviderKind == AgentProviderKind.Debug &&
            agent.ModelName == "debug-echo");
        (await model.OperationMessage).Should().Be("Created Debug Agent using Debug Provider.");
        (await model.Builder)!.StatusMessage.Should().Be("Ready to create an agent with Debug Provider.");
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
        await workspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None);
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
