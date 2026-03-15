using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

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
        draft.Role.Should().Be(AgentRoleKind.Reviewer);
        draft.ProviderKind.Should().Be(AgentProviderKind.Debug);
        draft.ModelName.Should().Be("debug-echo");
        draft.Capabilities.Should().Contain(AgentSessionDefaults.GitCapability);
        draft.Capabilities.Should().Contain(AgentSessionDefaults.FilesCapability);
        draft.SystemPrompt.Should().Contain("Mission:");
    }

    [Test]
    public async Task CreateManualDraftAsyncUsesDefaultDebugFallback()
    {
        await using var fixture = CreateFixture();

        var draft = await fixture.Generator.CreateManualDraftAsync(CancellationToken.None);

        draft.Name.Should().Be("New agent");
        draft.Role.Should().Be(AgentRoleKind.Operator);
        draft.ProviderKind.Should().Be(AgentProviderKind.Debug);
        draft.ModelName.Should().Be("debug-echo");
        draft.Capabilities.Should().Contain(AgentSessionDefaults.ShellCapability);
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
