using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using DotPilot.Core.ControlPlaneDomain;
using DotPilot.Tests.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.ChatSessions;

[NonParallelizable]
public sealed class AgentSessionServiceTests
{
    private const int LegacyDefaultRole = 4;
    private const string LegacyEmptyCapabilitiesJson = "[]";

    [Test]
    public async Task GetWorkspaceAsyncSeedsDefaultSystemAgentForANewStore()
    {
        await using var fixture = CreateFixture();

        var workspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

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
    public async Task CreateAgentAsyncPersistsLegacyCompatibilityColumnsInSqliteStore()
    {
        var tempRoot = CreateTempRootDirectory();
        var databasePath = Path.Combine(tempRoot, "legacy-store.db");
        await CreateSchemaAsync(databasePath, includeLegacyColumns: true);
        await using var fixture = CreateFixture(CreateSqliteOptions(tempRoot, databasePath), tempRoot);

        var created = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "SQLite Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Use the persisted SQLite path.",
                "SQLite-backed debug agent."),
            CancellationToken.None)).ShouldSucceed();

        created.Name.Should().Be("SQLite Agent");
        created.Description.Should().Be("SQLite-backed debug agent.");

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(CancellationToken.None);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT "Description", "Role", "CapabilitiesJson"
            FROM "AgentProfiles"
            WHERE "Name" = 'SQLite Agent';
            """;

        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        (await reader.ReadAsync(CancellationToken.None)).Should().BeTrue();
        reader.GetString(0).Should().Be("SQLite-backed debug agent.");
        reader.GetInt32(1).Should().Be(LegacyDefaultRole);
        reader.GetString(2).Should().Be(LegacyEmptyCapabilitiesJson);
    }

    [Test]
    public async Task GetWorkspaceAsyncUpgradesRegressedSqliteSchemaMissingLegacyColumns()
    {
        var tempRoot = CreateTempRootDirectory();
        var databasePath = Path.Combine(tempRoot, "regressed-store.db");
        await CreateSchemaAsync(databasePath, includeLegacyColumns: false);
        await using var fixture = CreateFixture(CreateSqliteOptions(tempRoot, databasePath), tempRoot);

        var workspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        workspace.Agents.Should().ContainSingle(agent => agent.Name == AgentSessionDefaults.SystemAgentName);

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(CancellationToken.None);

        await using var command = connection.CreateCommand();
        command.CommandText = """PRAGMA table_info("AgentProfiles")""";

        List<string> columns = [];
        await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
        var nameOrdinal = reader.GetOrdinal("name");
        while (await reader.ReadAsync(CancellationToken.None))
        {
            columns.Add(reader.GetString(nameOrdinal));
        }

        columns.Should().Contain("Role");
        columns.Should().Contain("CapabilitiesJson");
        columns.Should().Contain("Description");
    }

    [Test]
    public async Task CreateAgentAsyncPersistsAnEnabledDebugProviderProfile()
    {
        await using var fixture = CreateFixture();
        (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None)).ShouldSucceed();

        var created = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Debug Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Act as a deterministic local test agent.",
                "Deterministic local test agent."),
            CancellationToken.None)).ShouldSucceed();

        var workspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        created.Name.Should().Be("Debug Agent");
        created.Description.Should().Be("Deterministic local test agent.");
        created.ProviderKind.Should().Be(AgentProviderKind.Debug);
        workspace.Agents.Should().ContainSingle(agent => agent.Id == created.Id);
        workspace.Providers.Should().ContainSingle(provider =>
            provider.Kind == AgentProviderKind.Debug &&
            provider.IsEnabled &&
            provider.CanCreateAgents);
    }

    [Test]
    public async Task UpdateAgentAsyncUpdatesAnExistingProfileWithoutCreatingADuplicate()
    {
        await using var fixture = CreateFixture();
        var created = await EnableDebugAndCreateAgentAsync(fixture.Service, "Editable Agent");

        var updated = (await fixture.Service.UpdateAgentAsync(
            new UpdateAgentProfileCommand(
                created.Id,
                "Edited Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic after edit.",
                "Updated deterministic profile."),
            CancellationToken.None)).ShouldSucceed();

        var workspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        updated.Id.Should().Be(created.Id);
        updated.Name.Should().Be("Edited Agent");
        updated.Description.Should().Be("Updated deterministic profile.");
        workspace.Agents.Should().ContainSingle(agent =>
            agent.Id == created.Id &&
            agent.Name == "Edited Agent" &&
            agent.Description == "Updated deterministic profile." &&
            agent.SystemPrompt == "Stay deterministic after edit.");
    }

    [Test]
    public async Task CreateSessionAsyncCreatesInitialTranscriptState()
    {
        await using var fixture = CreateFixture();
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Session Agent");

        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Session with Session Agent", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        session.Session.Title.Should().Be("Session with Session Agent");
        session.Entries.Should().ContainSingle(entry =>
            entry.Kind == SessionStreamEntryKind.Status &&
            entry.Text.Contains("Session created", StringComparison.Ordinal));
    }

    [Test]
    public async Task CreateAgentAndSendMessageFlowPreservesSelectedModelAcrossConversation()
    {
        await using var fixture = CreateFixture();
        (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None)).ShouldSucceed();

        var agent = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Operator Flow Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Stay deterministic for operator-flow tests."),
            CancellationToken.None)).ShouldSucceed();

        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Operator flow session", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "what model are you using?"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry.ShouldSucceed());
        }

        var transcript = (await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None)).ShouldSucceed();

        transcript.Participants.Should().ContainSingle(participant =>
            participant.Id == agent.Id &&
            participant.ModelName == "debug-echo");
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: what model are you using?", StringComparison.Ordinal));
    }

    [Test]
    public async Task SendMessageAsyncStreamsDebugEntriesAndPersistsTranscript()
    {
        await using var fixture = CreateFixture();
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Streaming Agent");
        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Streaming session", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello from tests"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry.ShouldSucceed());
        }

        var reloaded = (await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None)).ShouldSucceed();

        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.UserMessage);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: hello from tests", StringComparison.Ordinal));

        reloaded.Entries.Should().Contain(entry =>
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
        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Transient session", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello from transient tests"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry.ShouldSucceed());
        }

        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Debug provider received: hello from transient tests", StringComparison.Ordinal));
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.ToolCompleted &&
            entry.Text.Contains("Debug workflow finished", StringComparison.Ordinal));
    }

    [Test]
    public async Task SendMessageAsyncReturnsProviderReadinessErrorWhenCodexCliIsMissing()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentSessionServiceTests));
        await using var fixture = CreateFixture();
        (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        var legacyAgentId = Guid.CreateVersion7();
        await SeedLegacyAgentAsync(fixture.Provider, legacyAgentId);

        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Legacy session", new AgentProfileId(legacyAgentId)),
            CancellationToken.None)).ShouldSucceed();

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello legacy"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry.ShouldSucceed());
        }

        var reloaded = (await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None)).ShouldSucceed();

        streamedEntries.Should().Contain(entry => entry.Kind == SessionStreamEntryKind.UserMessage);
        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.Error &&
            entry.Text.Contains("Codex CLI is not installed.", StringComparison.Ordinal));
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.AssistantMessage);

        reloaded.Entries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.Error &&
            entry.Text.Contains("Codex CLI is not installed.", StringComparison.Ordinal));
    }

    [Test]
    public async Task GetWorkspaceAsyncNormalizesLegacyDebugModelAssignedToANonDebugProvider()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentSessionServiceTests));
        await using var fixture = CreateFixture();
        var legacyAgentId = Guid.CreateVersion7();
        await SeedLegacyAgentAsync(
            fixture.Provider,
            legacyAgentId,
            AgentProviderKind.Codex,
            "Debug Agent",
            "debug-echo");
        (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None)).ShouldSucceed();

        var workspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();

        workspace.Agents.Should().ContainSingle(agent =>
            agent.Id == new AgentProfileId(legacyAgentId) &&
            agent.ProviderKind == AgentProviderKind.Codex &&
            !string.Equals(
                agent.ModelName,
                AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug),
                StringComparison.Ordinal));
    }

    [Test]
    public async Task GetWorkspaceAsyncReusesCachedProviderSnapshotAfterWarmRead()
    {
        using var commandScope = CodexCliTestScope.Create(nameof(AgentSessionServiceTests));
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");
        commandScope.WriteCodexMetadata("gpt-5.4", "gpt-5.4");
        await using var fixture = CreateFixture();

        var initialWorkspace = (await fixture.Service.GetWorkspaceAsync(CancellationToken.None)).ShouldSucceed();
        initialWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");

        commandScope.WriteCountingVersionCommand("codex", "codex version 2.0.0", delayMilliseconds: 300);
        commandScope.WriteCodexMetadata("gpt-5.1", "gpt-5.1");
        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var cachedWorkspace = (await fixture.Service.GetWorkspaceAsync(cancellationSource.Token)).ShouldSucceed();
        cachedWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");
    }

    [Test]
    public async Task GetSessionAsyncPropagatesCallerCancellation()
    {
        await using var fixture = CreateFixture();
        var agent = await EnableDebugAndCreateAgentAsync(fixture.Service, "Cancellation Agent");
        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Cancellation session", agent.Id),
            CancellationToken.None)).ShouldSucceed();
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        _ = Assert.ThrowsAsync<OperationCanceledException>(async () =>
            _ = await fixture.Service.GetSessionAsync(session.Session.Id, cancellationSource.Token));
    }

    private static async Task<AgentProfileSummary> EnableDebugAndCreateAgentAsync(
        IAgentSessionService service,
        string name)
    {
        (await service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None)).ShouldSucceed();

        return (await service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                name,
                AgentProviderKind.Debug,
                "debug-echo",
                "Be deterministic for automated verification.",
                "Deterministic local test agent."),
            CancellationToken.None)).ShouldSucceed();
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

    private static TestFixture CreateFixture(AgentSessionStorageOptions options, string tempRootPath)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(options);

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAgentSessionService>();
        return new TestFixture(provider, service, tempRootPath);
    }

    private static async Task SeedLegacyAgentAsync(
        ServiceProvider provider,
        Guid agentId,
        AgentProviderKind providerKind = AgentProviderKind.Codex,
        string agentName = "Legacy Codex Agent",
        string modelName = "gpt-5")
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
        SetProperty(record, "Name", agentName);
        TrySetProperty(record, "Description", string.Empty);
        SetProperty(record, "ProviderKind", (int)providerKind);
        SetProperty(record, "ModelName", modelName);
        SetProperty(record, "SystemPrompt", "Use Codex when available.");
        TrySetProperty(record, "Role", LegacyDefaultRole);
        TrySetProperty(record, "CapabilitiesJson", LegacyEmptyCapabilitiesJson);
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

    private static void TrySetProperty(object instance, string propertyName, object value)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var property = instance.GetType().GetProperty(propertyName);
        property?.SetValue(instance, value);
    }

    private static AgentSessionStorageOptions CreateSqliteOptions(string tempRootPath, string databasePath)
    {
        return new AgentSessionStorageOptions
        {
            DatabasePath = databasePath,
            RuntimeSessionDirectoryPath = Path.Combine(tempRootPath, "runtime"),
            ChatHistoryDirectoryPath = Path.Combine(tempRootPath, "history"),
            PlaygroundDirectoryPath = Path.Combine(tempRootPath, "playgrounds"),
        };
    }

    private static string CreateTempRootDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dotpilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task CreateSchemaAsync(string databasePath, bool includeLegacyColumns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(CancellationToken.None);

        var agentProfilesTableDefinition = includeLegacyColumns
            ? """
              CREATE TABLE "AgentProfiles" (
                  "Id" TEXT NOT NULL CONSTRAINT "PK_AgentProfiles" PRIMARY KEY,
                  "Name" TEXT NOT NULL,
                  "Description" TEXT NOT NULL,
                  "Role" INTEGER NOT NULL,
                  "ProviderKind" INTEGER NOT NULL,
                  "ModelName" TEXT NOT NULL,
                  "SystemPrompt" TEXT NOT NULL,
                  "CapabilitiesJson" TEXT NOT NULL,
                  "CreatedAt" TEXT NOT NULL
              );
              """
            : """
              CREATE TABLE "AgentProfiles" (
                  "Id" TEXT NOT NULL CONSTRAINT "PK_AgentProfiles" PRIMARY KEY,
                  "Name" TEXT NOT NULL,
                  "ProviderKind" INTEGER NOT NULL,
                  "ModelName" TEXT NOT NULL,
                  "SystemPrompt" TEXT NOT NULL,
                  "CreatedAt" TEXT NOT NULL
              );
              """;

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
              {agentProfilesTableDefinition}
              CREATE TABLE "Sessions" (
                  "Id" TEXT NOT NULL CONSTRAINT "PK_Sessions" PRIMARY KEY,
                  "Title" TEXT NOT NULL,
                  "PrimaryAgentProfileId" TEXT NOT NULL,
                  "CreatedAt" TEXT NOT NULL,
                  "UpdatedAt" TEXT NOT NULL
              );
              CREATE TABLE "SessionEntries" (
                  "Id" TEXT NOT NULL CONSTRAINT "PK_SessionEntries" PRIMARY KEY,
                  "SessionId" TEXT NOT NULL,
                  "AgentProfileId" TEXT NULL,
                  "Kind" INTEGER NOT NULL,
                  "Author" TEXT NOT NULL,
                  "Text" TEXT NOT NULL,
                  "AccentLabel" TEXT NULL,
                  "Timestamp" TEXT NOT NULL
              );
              CREATE TABLE "ProviderPreferences" (
                  "ProviderKind" INTEGER NOT NULL CONSTRAINT "PK_ProviderPreferences" PRIMARY KEY,
                  "IsEnabled" INTEGER NOT NULL,
                  "UpdatedAt" TEXT NOT NULL
              );
              CREATE INDEX "IX_Sessions_UpdatedAt" ON "Sessions" ("UpdatedAt");
              CREATE INDEX "IX_SessionEntries_SessionId_Timestamp" ON "SessionEntries" ("SessionId", "Timestamp");
              """;
        _ = await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentSessionService service, string? tempRootPath = null) : IAsyncDisposable
    {
        private readonly ServiceProvider _provider = provider;
        private readonly string? _tempRootPath = tempRootPath;

        public ServiceProvider Provider { get; } = provider;

        public IAgentSessionService Service { get; } = service;

        public ValueTask DisposeAsync()
        {
            return DisposeAsyncCore();
        }

        private async ValueTask DisposeAsyncCore()
        {
            await _provider.DisposeAsync();
            if (!string.IsNullOrWhiteSpace(_tempRootPath) && Directory.Exists(_tempRootPath))
            {
                Directory.Delete(_tempRootPath, recursive: true);
            }
        }
    }
}
