using DotPilot.Core.ControlPlaneDomain;
using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Tests.ChatSessions;

public sealed class AgentSessionServiceTests
{
    [Test]
    public async Task GetWorkspaceAsyncSeedsDefaultSystemAgentForANewStore()
    {
        await using var fixture = CreateFixture();

        var workspace = await fixture.Service.GetWorkspaceAsync(CancellationToken.None);

        workspace.Sessions.Should().BeEmpty();
        workspace.Agents.Should().ContainSingle(agent =>
            agent.Name == AgentSessionDefaults.SystemAgentName &&
            agent.ProviderKind == AgentProviderKind.Debug &&
            agent.ModelName == AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug));
        workspace.Providers.Should().HaveCount(4);
        workspace.Providers.Should().ContainSingle(provider => provider.Kind == AgentProviderKind.Debug);
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            provider.IsEnabled &&
            provider.CanCreateAgents);
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

    [Test]
    public async Task SendMessageAsyncStreamsDebugEntriesWhenTransientRuntimeConversationIsPreferred()
    {
        await using var fixture = CreateFixture(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
            PreferTransientRuntimeConversation = true,
        });
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Transient Agent");
        var session = await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Transient session", agent.Id),
            CancellationToken.None);

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello from transient tests"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry);
        }

        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: hello from transient tests", StringComparison.Ordinal));
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.ToolCompleted &&
            entry.Text.Contains("Debug workflow finished", StringComparison.Ordinal));
    }

    [Test]
    public async Task LegacyUnsupportedProviderSessionReturnsExplicitErrorWithoutRuntimePlaceholderClient()
    {
        await using var fixture = CreateFixture();
        await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None);

        var legacyAgentId = Guid.CreateVersion7();
        await SeedLegacyAgentAsync(fixture.Provider, legacyAgentId);

        var session = await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Legacy session", new AgentProfileId(legacyAgentId)),
            CancellationToken.None);

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello legacy"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry);
        }

        var reloaded = await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None);

        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.UserMessage);
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.Error &&
            entry.Text.Contains("Codex live CLI execution is not wired yet in this slice.", StringComparison.Ordinal));
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.AssistantMessage);

        reloaded.Should().NotBeNull();
        reloaded!.Entries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.Error &&
            entry.Text.Contains("Codex live CLI execution is not wired yet in this slice.", StringComparison.Ordinal));
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

    private static TestFixture CreateFixture(AgentSessionStorageOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(options ?? new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAgentSessionService>();
        return new TestFixture(provider, service);
    }

    private static async Task SeedLegacyAgentAsync(ServiceProvider provider, Guid agentId)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var serviceAssembly = provider.GetRequiredService<IAgentSessionService>().GetType().Assembly;
        var dbContextType = serviceAssembly.GetType("DotPilot.Core.ChatSessions.LocalAgentSessionDbContext")
            ?? throw new InvalidOperationException("LocalAgentSessionDbContext type was not found.");
        var agentProfileRecordType = serviceAssembly.GetType("DotPilot.Core.ChatSessions.AgentProfileRecord")
            ?? throw new InvalidOperationException("AgentProfileRecord type was not found.");
        var dbContextFactoryType = typeof(IDbContextFactory<>).MakeGenericType(dbContextType);
        var dbContextFactory = provider.GetRequiredService(dbContextFactoryType);
        var createDbContextMethod = dbContextFactoryType.GetMethod("CreateDbContext", Type.EmptyTypes)
            ?? throw new InvalidOperationException("CreateDbContext method was not found.");

        await using var dbContext = (DbContext)(createDbContextMethod.Invoke(dbContextFactory, []) ??
            throw new InvalidOperationException("CreateDbContext returned null."));

        var record = Activator.CreateInstance(agentProfileRecordType)
            ?? throw new InvalidOperationException("AgentProfileRecord could not be created.");
        SetProperty(record, "Id", agentId);
        SetProperty(record, "Name", "Legacy Codex Agent");
        SetProperty(record, "Role", (int)AgentRoleKind.Operator);
        SetProperty(record, "ProviderKind", (int)AgentProviderKind.Codex);
        SetProperty(record, "ModelName", "gpt-5");
        SetProperty(record, "SystemPrompt", "Use Codex when available.");
        SetProperty(record, "CapabilitiesJson", "[]");
        SetProperty(record, "CreatedAt", DateTimeOffset.UtcNow);

        dbContext.Add(record);
        _ = await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static void SetProperty(object instance, string propertyName, object value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var property = instance.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on '{instance.GetType().FullName}'.");
        property.SetValue(instance, value);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public TestFixture(ServiceProvider provider, IAgentSessionService service)
        {
            _provider = provider;
            Provider = provider;
            Service = service;
        }

        public ServiceProvider Provider { get; }

        public IAgentSessionService Service { get; }

        public ValueTask DisposeAsync()
        {
            return _provider.DisposeAsync();
        }
    }
}
