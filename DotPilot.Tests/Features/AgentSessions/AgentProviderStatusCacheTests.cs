using DotPilot.Core.Features.AgentSessions;
using DotPilot.Runtime.Features.AgentSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.AgentSessions;

[NonParallelizable]
public sealed class AgentProviderStatusCacheTests
{
    [Test]
    public async Task GetWorkspaceAsyncReturnsCachedProviderStatusUntilRefreshRequested()
    {
        using var commandScope = CommandProbeScope.Create();
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");

        await using var fixture = CreateFixture();
        var initial = await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None);

        initial.InstalledVersion.Should().Be("1.0.0");

        commandScope.WriteVersionCommand("codex", "codex version 2.0.0");

        var cachedWorkspace = await fixture.Service.GetWorkspaceAsync(CancellationToken.None);
        cachedWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("1.0.0");

        await fixture.ProviderStatusCache.RefreshAsync(CancellationToken.None);

        var refreshedWorkspace = await fixture.Service.GetWorkspaceAsync(CancellationToken.None);
        refreshedWorkspace.Providers
            .Single(provider => provider.Kind == AgentProviderKind.Codex)
            .InstalledVersion
            .Should()
            .Be("2.0.0");
    }

    [Test]
    public async Task EnabledExternalProviderRemainsUnavailableUntilLiveRuntimeIsWired()
    {
        using var commandScope = CommandProbeScope.Create();
        commandScope.WriteVersionCommand("codex", "codex version 1.0.0");

        await using var fixture = CreateFixture();
        var provider = await fixture.Service.UpdateProviderAsync(
            new UpdateProviderPreferenceCommand(AgentProviderKind.Codex, true),
            CancellationToken.None);

        provider.IsEnabled.Should().BeTrue();
        provider.CanCreateAgents.Should().BeFalse();
        provider.Status.Should().Be(AgentProviderStatus.Error);
        provider.StatusSummary.Should().Contain("not wired yet");
        provider.InstalledVersion.Should().Be("1.0.0");
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
        return new TestFixture(
            provider,
            provider.GetRequiredService<IAgentSessionService>(),
            provider.GetRequiredService<IAgentProviderStatusCache>());
    }

    private sealed class TestFixture(
        ServiceProvider provider,
        IAgentSessionService service,
        IAgentProviderStatusCache providerStatusCache)
        : IAsyncDisposable
    {
        public IAgentSessionService Service { get; } = service;

        public IAgentProviderStatusCache ProviderStatusCache { get; } = providerStatusCache;

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
                nameof(AgentProviderStatusCacheTests),
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

        public void WriteVersionCommand(string commandName, string output)
        {
            var commandPath = OperatingSystem.IsWindows()
                ? Path.Combine(_rootPath, commandName + ".cmd")
                : Path.Combine(_rootPath, commandName);

            var commandBody = OperatingSystem.IsWindows()
                ? $"@echo off{Environment.NewLine}echo {output}{Environment.NewLine}"
                : $"#!/bin/sh{Environment.NewLine}echo \"{output}\"{Environment.NewLine}";

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
    }
}
