using System.Diagnostics;
using DotPilot.Core;
using DotPilot.Core.ChatSessions;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Host.Power;

public sealed class DesktopSleepPreventionServiceTests
{
    [Test]
    public async Task ServiceTracksSessionActivityLifecycle()
    {
        if (OperatingSystem.IsLinux() && !CommandExists("systemd-inhibit"))
        {
            Assert.Ignore("systemd-inhibit is not available on this machine.");
        }

        if (OperatingSystem.IsMacOS() && !CommandExists("caffeinate"))
        {
            Assert.Ignore("caffeinate is not available on this machine.");
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAgentSessions(new AgentSessionStorageOptions
        {
            UseInMemoryDatabase = true,
            InMemoryDatabaseName = Guid.NewGuid().ToString("N"),
        });
        services.AddSingleton<DesktopSleepPreventionService>();

        await using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<ISessionActivityMonitor>();
        var sleepPrevention = provider.GetRequiredService<DesktopSleepPreventionService>();

        using var lease = monitor.BeginActivity(
            new SessionActivityDescriptor(
                SessionId.New(),
                "Sleep prevention session",
                AgentProfileId.New(),
                "Sleep agent",
                "Debug Provider"));

        await WaitForAsync(static service => service.IsSleepPreventionActive, sleepPrevention);

        lease.Dispose();

        await WaitForAsync(static service => !service.IsSleepPreventionActive, sleepPrevention);
    }

    private static async Task WaitForAsync(
        Func<DesktopSleepPreventionService, bool> predicate,
        DesktopSleepPreventionService service)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (predicate(service))
            {
                return;
            }

            await Task.Delay(50);
        }

        predicate(service).Should().BeTrue();
    }

    private static bool CommandExists(string commandName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"command -v {commandName}");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
