using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.AgentBuilder;

public sealed class AgentPromptDraftGeneratorTests
{
    [Test]
    public async Task GenerateAsyncCreatesRepositoryReviewDraftUsingCreatableProvider()
    {
        await using var fixture = CreateFixture();

        var draft = await fixture.Generator.GenerateAsync(
            "Create a repository reviewer that checks git diff before answering.",
            CancellationToken.None);

        draft.Name.Should().Be("Repository Reviewer Checks Agent");
        draft.ProviderKind.Should().Be(AgentProviderKind.Debug);
        draft.ModelName.Should().Be("debug-echo");
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
            provider.GetRequiredService<AgentPromptDraftGenerator>());
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        AgentPromptDraftGenerator generator) : IAsyncDisposable
    {
        public AgentPromptDraftGenerator Generator { get; } = generator;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
