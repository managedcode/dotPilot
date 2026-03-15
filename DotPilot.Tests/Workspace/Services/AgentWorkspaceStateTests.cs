using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Workspace;

[NonParallelizable]
public sealed class AgentWorkspaceStateTests
{
    [Test]
    public async Task ConcurrentColdWorkspaceReadsOnlyProbeProviderStatusOnce()
    {
        using var commandScope = CommandProbeScope.Create();
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");

        await using var fixture = CreateFixture();

        await Task.WhenAll(
            Enumerable.Range(0, 4)
                .Select(_ => fixture.WorkspaceState.GetWorkspaceAsync(CancellationToken.None).AsTask()));

        commandScope.ReadInvocationCount("codex").Should().Be(1);
    }

    private static TestFixture CreateFixture()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });

        var provider = services.BuildServiceProvider();
        return new TestFixture(provider, provider.GetRequiredService<IAgentWorkspaceState>());
    }

    private sealed class TestFixture(ServiceProvider provider, IAgentWorkspaceState workspaceState) : IAsyncDisposable
    {
        public IAgentWorkspaceState WorkspaceState { get; } = workspaceState;

        public ValueTask DisposeAsync()
        {
            return provider.DisposeAsync();
        }
    }

    private sealed class CommandProbeScope : IDisposable
    {
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
                nameof(AgentWorkspaceStateTests),
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

        public int ReadInvocationCount(string commandName)
        {
            var callCountPath = GetCallCountPath(commandName);
            return File.Exists(callCountPath)
                ? File.ReadAllLines(callCountPath).Length
                : 0;
        }

        public void WriteVersionCommand(string commandName, string output)
        {
            var commandPath = OperatingSystem.IsWindows()
                ? Path.Combine(_rootPath, commandName + ".cmd")
                : Path.Combine(_rootPath, commandName);
            var callCountPath = GetCallCountPath(commandName);
            var commandBody = OperatingSystem.IsWindows()
                ? $"@echo off{Environment.NewLine}echo called>>\"{callCountPath}\"{Environment.NewLine}echo {output}{Environment.NewLine}"
                : $"#!/bin/sh{Environment.NewLine}echo called >> \"{callCountPath}\"{Environment.NewLine}echo \"{output}\"{Environment.NewLine}";

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

        private string GetCallCountPath(string commandName)
        {
            return Path.Combine(_rootPath, commandName + ".calls");
        }
    }
}
