using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using DotPilot.Core;
using DotPilot.Core.ChatSessions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.ChatSessions;

public sealed class AgentSessionPersistenceTests
{
    private const int DeleteRetryCount = 40;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions HistorySerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    [Test]
    public async Task SendMessageAsyncPersistsFolderBackedAgentSessionAndHistoryAcrossServiceRestart()
    {
        var root = CreateRootPath();

        try
        {
            SessionId sessionId;
            var storageOptions = CreateStorageOptions(root);

            await using (var firstFixture = CreateFixture(storageOptions))
            {
                var agent = await EnableDebugAndCreateAgentAsync(firstFixture.Service, "Persistent Agent");
                var session = (await firstFixture.Service.CreateSessionAsync(
                    new CreateSessionCommand("Persistent session", agent.Id),
                    CancellationToken.None)).ShouldSucceed();
                sessionId = session.Session.Id;

                await DrainAsync(
                    firstFixture.Service.SendMessageAsync(
                        new SendSessionMessageCommand(sessionId, "first persisted prompt"),
                        CancellationToken.None));
            }

            var sessionFile = Path.Combine(
                storageOptions.RuntimeSessionDirectoryPath!,
                sessionId.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture) + ".json");
            var historyFile = Path.Combine(
                storageOptions.ChatHistoryDirectoryPath!,
                sessionId.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture) + ".json");

            File.Exists(sessionFile).Should().BeTrue();
            File.Exists(historyFile).Should().BeTrue();

            var firstHistory = await ReadHistoryAsync(historyFile);
            firstHistory.Should().ContainSingle(message =>
                message.Role == ChatRole.User &&
                message.Text == "first persisted prompt");
            firstHistory.Should().ContainSingle(message =>
                message.Role == ChatRole.Assistant &&
                message.Text.Contains("Debug provider received: first persisted prompt", StringComparison.Ordinal));

            await using (var secondFixture = CreateFixture(storageOptions))
            {
                var reloaded = (await secondFixture.Service.GetSessionAsync(sessionId, CancellationToken.None)).ShouldSucceed();
                reloaded.Entries.Should().Contain(entry =>
                    entry.Kind == SessionStreamEntryKind.AssistantMessage &&
                    entry.Text.Contains("Debug provider received: first persisted prompt", StringComparison.Ordinal));

                await DrainAsync(
                    secondFixture.Service.SendMessageAsync(
                        new SendSessionMessageCommand(sessionId, "second persisted prompt"),
                        CancellationToken.None));
            }

            var secondHistory = await ReadHistoryAsync(historyFile);
            secondHistory.Should().ContainSingle(message =>
                message.Role == ChatRole.User &&
                message.Text == "first persisted prompt");
            secondHistory.Should().ContainSingle(message =>
                message.Role == ChatRole.User &&
                message.Text == "second persisted prompt");
            secondHistory.Should().Contain(message =>
                message.Role == ChatRole.Assistant &&
                message.Text.Contains("Debug provider received: second persisted prompt", StringComparison.Ordinal));
        }
        finally
        {
            await DeleteDirectoryAsync(root);
        }
    }

    private static AgentSessionStorageOptions CreateStorageOptions(string root)
    {
        return new AgentSessionStorageOptions
        {
            DatabasePath = Path.Combine(root, "sqlite", "agent-sessions.db"),
            RuntimeSessionDirectoryPath = Path.Combine(root, "runtime-sessions"),
            ChatHistoryDirectoryPath = Path.Combine(root, "chat-history"),
        };
    }

    private static TestFixture CreateFixture(AgentSessionStorageOptions storageOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(storageOptions);

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAgentSessionService>();
        return new TestFixture(provider, service);
    }

    private static async Task DrainAsync(IAsyncEnumerable<ManagedCode.Communication.Result<SessionStreamEntry>> stream)
    {
        await foreach (var result in stream)
        {
            result.ShouldSucceed();
        }
    }

    private static async Task<IReadOnlyList<ChatMessage>> ReadHistoryAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var messages = await JsonSerializer.DeserializeAsync<ChatMessage[]>(
            stream,
            HistorySerializerOptions,
            CancellationToken.None);

        return messages ?? [];
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
                "Be deterministic for automated verification."),
            CancellationToken.None)).ShouldSucceed();
    }

    private static string CreateRootPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            nameof(AgentSessionPersistenceTests),
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task DeleteDirectoryAsync(string path)
    {
        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            SqliteConnection.ClearAllPools();
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay);
            }
        }
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
