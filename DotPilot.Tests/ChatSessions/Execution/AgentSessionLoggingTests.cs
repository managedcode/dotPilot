using System.Collections.Concurrent;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotPilot.Tests.ChatSessions;

public sealed class AgentSessionLoggingTests
{
    [Test]
    public async Task RuntimeFlowsEmitLifecycleLogsForAgentSessionAndSend()
    {
        var recordingProvider = new RecordingLoggerProvider();
        await using var fixture = CreateFixture(recordingProvider);

        (await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Debug, true),
            CancellationToken.None)).ShouldSucceed();

        var agent = (await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Logged Agent",
                AgentProviderKind.Debug,
                "debug-echo",
                "Be explicit in tests."),
            CancellationToken.None)).ShouldSucceed();

        var session = (await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Logged session", agent.Id),
            CancellationToken.None)).ShouldSucceed();

        await foreach (var result in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "hello logs"),
                           CancellationToken.None))
        {
            result.ShouldSucceed();
        }

        var messages = recordingProvider.Entries
            .Select(entry => entry.Message)
            .ToArray();

        messages.Should().Contain(message => message.Contains("Created agent profile.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Created session.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Starting session send.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Configured agent run middleware.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Configured run-scoped chat logging.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Prepared correlated agent run.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Agent run started.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Agent run completed.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Chat client request started.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Chat client request completed.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("RunId=", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Logged Agent", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Completed session send.", StringComparison.Ordinal));
        messages.Should().Contain(message => message.Contains("Provider probe completed.", StringComparison.Ordinal));
    }

    private static TestFixture CreateFixture(RecordingLoggerProvider recordingProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddProvider(recordingProvider);
        });
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(provider, provider.GetRequiredService<IAgentSessionService>());
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentSessionService service) : IAsyncDisposable
    {
        public IAgentSessionService Service { get; } = service;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(categoryName, _entries);
        }

        public sealed record LogEntry(string CategoryName, LogLevel Level, string Message);

        private sealed class RecordingLogger(
            string categoryName,
            ConcurrentQueue<LogEntry> entries)
            : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Enqueue(new LogEntry(categoryName, logLevel, formatter(state, exception)));
            }
        }
    }
}
