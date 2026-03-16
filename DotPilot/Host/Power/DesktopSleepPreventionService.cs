using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotPilot;

public sealed class DesktopSleepPreventionService : IDisposable
{
    private const string LinuxInhibitReason = "dotPilot live session";
    private const string LinuxInhibitCommand = "sh";
    private const string LinuxInhibitScript = "while :; do sleep 3600; done";
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    private readonly ISessionActivityMonitor sessionActivityMonitor;
    private readonly ILogger<DesktopSleepPreventionService> logger;
    private readonly Lock gate = new();
    private Process? inhibitorProcess;
    private bool isSleepPreventionActive;

    public DesktopSleepPreventionService(
        ISessionActivityMonitor sessionActivityMonitor,
        ILogger<DesktopSleepPreventionService> logger)
    {
        this.sessionActivityMonitor = sessionActivityMonitor;
        this.logger = logger;
        this.sessionActivityMonitor.StateChanged += OnSessionActivityStateChanged;
        ApplySessionActivityState();
    }

    public event EventHandler? StateChanged;

    public bool IsSleepPreventionActive
    {
        get
        {
            lock (gate)
            {
                return isSleepPreventionActive;
            }
        }
    }

    public void Dispose()
    {
        sessionActivityMonitor.StateChanged -= OnSessionActivityStateChanged;
        ReleaseSleepPrevention();
    }

    private void OnSessionActivityStateChanged(object? sender, EventArgs e)
    {
        ApplySessionActivityState();
    }

    private void ApplySessionActivityState()
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        if (sessionActivityMonitor.Current.HasActiveSessions)
        {
            AcquireSleepPrevention();
            return;
        }

        ReleaseSleepPrevention();
    }

    private void AcquireSleepPrevention()
    {
        lock (gate)
        {
            if (isSleepPreventionActive)
            {
                return;
            }
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                AcquireWindowsSleepPrevention();
                SetActiveState("SetThreadExecutionState");
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                SetProcessState(StartMacOsInhibitorProcess());
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                SetProcessState(StartLinuxInhibitorProcess());
            }
        }
        catch (Exception exception)
        {
            ShellSleepPreventionLog.AcquireFailed(logger, exception);
            ReleaseSleepPrevention();
        }
    }

    private void ReleaseSleepPrevention()
    {
        Process? processToStop;
        bool wasActive;

        lock (gate)
        {
            if (!isSleepPreventionActive && inhibitorProcess is null)
            {
                return;
            }

            if (OperatingSystem.IsWindows() && isSleepPreventionActive)
            {
                ReleaseWindowsSleepPrevention();
            }

            processToStop = inhibitorProcess;
            inhibitorProcess = null;
            wasActive = isSleepPreventionActive;
            isSleepPreventionActive = false;
        }

        StopProcess(processToStop);
        if (!wasActive)
        {
            return;
        }

        ShellSleepPreventionLog.Released(logger);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetProcessState(Process process)
    {
        lock (gate)
        {
            inhibitorProcess = process;
            isSleepPreventionActive = true;
        }

        ShellSleepPreventionLog.Acquired(logger, process.ProcessName);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetActiveState(string mechanism)
    {
        lock (gate)
        {
            isSleepPreventionActive = true;
        }

        ShellSleepPreventionLog.Acquired(logger, mechanism);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void AcquireWindowsSleepPrevention()
    {
        var result = SetThreadExecutionState(EsContinuous | EsSystemRequired);
        if (result == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not acquire the Windows execution-state wake lock.");
        }
    }

    private static void ReleaseWindowsSleepPrevention()
    {
        _ = SetThreadExecutionState(EsContinuous);
    }

    private static Process StartMacOsInhibitorProcess()
    {
        return StartProcess("caffeinate", static arguments =>
        {
            arguments.Add("-i");
        });
    }

    private static Process StartLinuxInhibitorProcess()
    {
        return StartProcess("systemd-inhibit", static arguments =>
        {
            arguments.Add($"--why={LinuxInhibitReason}");
            arguments.Add("--what=sleep");
            arguments.Add("--mode=block");
            arguments.Add(LinuxInhibitCommand);
            arguments.Add("-c");
            arguments.Add(LinuxInhibitScript);
        });
    }

    private static Process StartProcess(string fileName, Action<IList<string>> configureArguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        configureArguments(startInfo.ArgumentList);

        return Process.Start(startInfo) ??
            throw new InvalidOperationException($"Could not start '{fileName}' for desktop sleep prevention.");
    }

    private static void StopProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint executionState);
#pragma warning restore SYSLIB1054
}
