using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using DotPilot.Tests.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.AgentBuilder;

[NonParallelizable]
public sealed class AgentPromptDraftGeneratorTests
{
    [Test]
    public async Task GenerateAsyncCreatesRepositoryReviewDraftUsingEnabledRealProvider()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentPromptDraftGeneratorTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4", "gpt-5");

        await using var fixture = CreateFixture();
        (await fixture.WorkspaceState.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        var draft = await fixture.Generator.GenerateAsync(
            "Create a repository reviewer that checks git diff before answering.",
            CancellationToken.None);

        draft.Name.Should().Be("Repository Reviewer Checks Agent");
        draft.ProviderKind.Should().Be(AgentProviderKind.Codex);
        draft.ModelName.Should().Be("gpt-5.4");
        draft.SystemPrompt.Should().Contain("Mission:");
        draft.SystemPrompt.Should().NotContain("Primary tools:");
        draft.SystemPrompt.Should().NotContain("Preferred skills:");
        draft.SystemPrompt.Should().NotContain("Role:");
    }

    [Test]
    public async Task CreateManualDraftAsyncUsesDefaultDebugFallback()
    {
        await using var fixture = CreateFixture();

        var draft = await fixture.Generator.CreateManualDraftAsync(CancellationToken.None);

        draft.Name.Should().Be("New agent");
        draft.ProviderKind.Should().Be(AgentProviderKind.Debug);
        draft.ModelName.Should().Be("debug-echo");
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(
            provider,
            provider.GetRequiredService<AgentPromptDraftGenerator>(),
            provider.GetRequiredService<IAgentWorkspaceState>());
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        AgentPromptDraftGenerator generator,
        IAgentWorkspaceState workspaceState) : IAsyncDisposable
    {
        public AgentPromptDraftGenerator Generator { get; } = generator;

        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
