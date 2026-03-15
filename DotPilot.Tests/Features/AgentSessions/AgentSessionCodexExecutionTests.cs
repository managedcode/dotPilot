using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

[NonParallelizable]
public sealed class AgentSessionCodexExecutionTests
{
    [Test]
    public async Task SendMessageAsyncUsesCodexCliWhenProviderIsEnabled()
    {
        using var commandScope = CommandProbeScope.Create();
        commandScope.WriteCodexCommand("1.0.0", "Stubbed Codex response.");

        await using var fixture = CreateFixture();
        await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None);

        var agent = await fixture.Service.CreateAgentAsync(
            new CreateAgentProfileCommand(
                "Codex Agent",
                AgentRoleKind.Operator,
                AgentProviderKind.Codex,
                "gpt-5",
                "Answer through the local codex CLI.",
                [AgentSessionDefaults.FilesCapability]),
            CancellationToken.None);
        var session = await fixture.Service.CreateSessionAsync(
            new CreateSessionCommand("Session with Codex Agent", agent.Id),
            CancellationToken.None);

        List<SessionStreamEntry> streamedEntries = [];
        await foreach (var entry in fixture.Service.SendMessageAsync(
                           new SendSessionMessageCommand(session.Session.Id, "say hello"),
                           CancellationToken.None))
        {
            streamedEntries.Add(entry);
        }

        var reloaded = await fixture.Service.GetSessionAsync(session.Session.Id, CancellationToken.None);

        streamedEntries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Stubbed Codex response.", StringComparison.Ordinal));
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.Error);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolStarted);
        streamedEntries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.ToolCompleted);

        reloaded.Should().NotBeNull();
        reloaded!.Entries.Should().Contain(entry =>
            entry.Kind == SessionStreamEntryKind.AssistantMessage &&
            entry.Text.Contains("Stubbed Codex response.", StringComparison.Ordinal));
        reloaded.Entries.Should().NotContain(entry => entry.Kind == SessionStreamEntryKind.Error);
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

    private sealed class CommandProbeScope : IDisposable
    {
        private const string VersionPlaceholder = "__VERSION__";
        private const string ResponsePlaceholder = "__RESPONSE__";
        private readonly string _rootPath;
        private readonly string? _originalPath;
        private bool _disposed;

        private CommandProbeScope(string rootPath, string? originalPath)
        {
            _rootPath = rootPath;
            _originalPath = originalPath;
        }

        public static CommandProbeScope Create()
        {
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            var rootPath = Path.Combine(
                Path.GetTempPath(),
                "DotPilot.Tests",
                nameof(AgentSessionCodexExecutionTests),
                Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(rootPath);
            Environment.SetEnvironmentVariable("PATH", rootPath);
            return new CommandProbeScope(rootPath, originalPath);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Environment.SetEnvironmentVariable("PATH", _originalPath);
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }

            _disposed = true;
        }

        public void WriteCodexCommand(string version, string response)
        {
            var commandPath = OperatingSystem.IsWindows()
                ? Path.Combine(_rootPath, "codex.cmd")
                : Path.Combine(_rootPath, "codex");

            var commandBody = OperatingSystem.IsWindows()
                ? CreateWindowsCodexScript(version, response)
                : CreateUnixCodexScript(version, response);
            File.WriteAllText(commandPath, commandBody);

            if (OperatingSystem.IsWindows())
            {
                return;
            }

            File.SetUnixFileMode(
                commandPath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute |
                UnixFileMode.GroupRead |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead |
                UnixFileMode.OtherExecute);
        }

        private static string CreateWindowsCodexScript(string version, string response)
        {
            return string.Join(
                Environment.NewLine,
                [
                    "@echo off",
                    "if \"%1\"==\"--version\" (",
                    $"    echo codex version {version}",
                    "    exit /b 0",
                    ")",
                    "if \"%1\"==\"exec\" (",
                    "    more >nul",
                    "    echo {\"type\":\"thread.started\",\"thread_id\":\"thread-1\"}",
                    "    echo {\"type\":\"turn.started\"}",
                    $"    echo {{\"type\":\"item.updated\",\"item\":{{\"type\":\"agent_message\",\"id\":\"msg-1\",\"text\":\"{response}\"}}}}",
                    $"    echo {{\"type\":\"item.completed\",\"item\":{{\"type\":\"agent_message\",\"id\":\"msg-1\",\"text\":\"{response}\"}}}}",
                    "    echo {\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1,\"cached_input_tokens\":0,\"output_tokens\":1}}",
                    "    exit /b 0",
                    ")",
                    "echo unsupported args 1>&2",
                    "exit /b 1",
                ]);
        }

        private static string CreateUnixCodexScript(string version, string response)
        {
            const string Template = """
#!/bin/sh
if [ "$1" = "--version" ]; then
  echo "codex version __VERSION__"
  exit 0
fi
if [ "$1" = "exec" ]; then
  cat >/dev/null
  printf '%s\n' '{"type":"thread.started","thread_id":"thread-1"}'
  printf '%s\n' '{"type":"turn.started"}'
  printf '%s\n' '{"type":"item.updated","item":{"type":"agent_message","id":"msg-1","text":"__RESPONSE__"}}'
  printf '%s\n' '{"type":"item.completed","item":{"type":"agent_message","id":"msg-1","text":"__RESPONSE__"}}'
  printf '%s\n' '{"type":"turn.completed","usage":{"input_tokens":1,"cached_input_tokens":0,"output_tokens":1}}'
  exit 0
fi
echo "unsupported args" >&2
exit 1
""";

            return Template
                .Replace(VersionPlaceholder, EscapeShellValue(version), StringComparison.Ordinal)
                .Replace(ResponsePlaceholder, EscapeJsonString(response), StringComparison.Ordinal);
        }

        private static string EscapeShellValue(string value)
        {
            return value.Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string EscapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
