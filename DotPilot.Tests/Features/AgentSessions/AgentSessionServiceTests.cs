using DotPilot.Core.Features.AgentSessions;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

public sealed class AgentSessionServiceTests
{
    [Test]
    public async Task GetWorkspaceAsyncReturnsProviderCatalogAndNoSessionsForNewStore()
    {
        await using var fixture = CreateFixture();

        var workspace = await fixture.Service.GetWorkspaceAsync(CancellationToken.None);

        workspace.Sessions.Should().BeEmpty();
        workspace.Agents.Should().BeEmpty();
        workspace.Providers.Should().HaveCount(4);
        workspace.Providers.Should().ContainSingle(provider => provider.Kind == AgentProviderKind.Debug);
        workspace.Providers.Should().OnlyContain(provider => !provider.IsEnabled);
    }

    [Test]
    public async Task CreateAgentAsyncPersistsAnEnabledDebugProviderProfile()
    {
        await using var fixture = CreateFixture();
        await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None);

        var created = await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Debug Agent",
                AgentRoleKind.Coding,
                AgentProviderKind.Debug,
                "debug-echo",
                "Act as a deterministic local test agent.",
                ["Shell", "Files"]),
            CancellationToken.None);

        var workspace = await fixture.Service.GetWorkspaceAsync(CancellationToken.None);

        created.Name.Should().Be("Debug Agent");
        created.ProviderKind.Should().Be(AgentProviderKind.Debug);
        workspace.Agents.Should().ContainSingle(agent => agent.Id == created.Id);
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            provider.IsEnabled &&
            provider.CanCreateAgents);
    }

    [Test]
    public async Task CreateSessionAsyncCreatesInitialTranscriptState()
    {
        await using var fixture = CreateFixture();
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Session Agent");

        var session = await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Session with Session Agent", agent.Id),
            CancellationToken.None);

        session.Session.Title.Should().Be("Session with Session Agent");
        session.Entries.Should().ContainSingle(entry =>
            entry.Kind == SessionStreamEntryKind.Status &&
            entry.Text.Contains("Session created", StringComparison.Ordinal));
    }

    [Test]
    public async Task SendMessageAsyncStreamsDebugEntriesAndPersistsTranscript()
    {
        await using var fixture = CreateFixture();
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Streaming Agent");
        var session = await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Streaming session", agent.Id),
            CancellationToken.None);

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello from tests"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry);
        }

        var reloaded = await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None);

        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.UserMessage);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: hello from tests", StringComparison.Ordinal));

        reloaded.Should().NotBeNull();
        reloaded!.Entries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: hello from tests", StringComparison.Ordinal));
        reloaded.Entries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.ToolCompleted &&
            entry.Text.Contains("Debug workflow finished", StringComparison.Ordinal));
    }

    private static async Task<AgentProfileSummary> EnableDebugAndCreateAgentAsync(
        IAgentSessionService service,
        string name)
    {
        await service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None);

        return await service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                name,
                AgentRoleKind.Operator,
                AgentProviderKind.Debug,
                "debug-echo",
                "Be deterministic for automated verification.",
                ["Shell"]),
            CancellationToken.None);
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
        var service = provider.GetRequiredService<IAgentSessionService>();
        return new TestFixture(provider, service);
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentSessionService service) : IAsyncDisposable
    {
        public IAgentSessionService Service { get; } = service;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }
}
