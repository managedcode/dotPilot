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
    private bool isSleepPreventionPending;
    private long stateVersion;

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
        var acquisition = TryBeginAcquisition();
        if (!acquisition.ShouldAcquire)
        {
            return;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                AcquireWindowsSleepPrevention();
                CompleteWindowsAcquisition(acquisition.Version, "SetThreadExecutionState");
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                CompleteProcessAcquisition(acquisition.Version, StartMacOsInhibitorProcess());
                return;
            }

            if (OperatingSystem.IsLinux())
            {
                CompleteProcessAcquisition(acquisition.Version, StartLinuxInhibitorProcess());
                return;
            }

            CancelPendingAcquisition(acquisition.Version);
        }
        catch (Exception exception)
        {
            ShellSleepPreventionLog.AcquireFailed(logger, exception);
            CancelPendingAcquisition(acquisition.Version);
            ReleaseSleepPrevention();
        }
    }

    private void ReleaseSleepPrevention()
    {
        Process? processToStop;
        bool shouldReleaseWindows;
        bool wasActive;

        lock (gate)
        {
            stateVersion++;
            if (!isSleepPreventionActive && !isSleepPreventionPending && inhibitorProcess is null)
            {
                return;
            }

            shouldReleaseWindows = OperatingSystem.IsWindows() && isSleepPreventionActive;
            processToStop = inhibitorProcess;
            inhibitorProcess = null;
            wasActive = isSleepPreventionActive;
            isSleepPreventionActive = false;
            isSleepPreventionPending = false;
        }

        if (shouldReleaseWindows)
        {
            ReleaseWindowsSleepPrevention();
        }

        StopProcess(processToStop);
        if (!wasActive)
        {
            return;
        }

        ShellSleepPreventionLog.Released(logger);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private (bool ShouldAcquire, long Version) TryBeginAcquisition()
    {
        lock (gate)
        {
            if (isSleepPreventionActive || isSleepPreventionPending)
            {
                return (false, stateVersion);
            }

            stateVersion++;
            isSleepPreventionPending = true;
            return (true, stateVersion);
        }
    }

    private void CompleteProcessAcquisition(long version, Process process)
    {
        var acquired = false;
        lock (gate)
        {
            if (isSleepPreventionPending && !isSleepPreventionActive && version == stateVersion)
            {
                inhibitorProcess = process;
                isSleepPreventionActive = true;
                isSleepPreventionPending = false;
                acquired = true;
            }
        }

        if (!acquired)
        {
            StopProcess(process);
            return;
        }

        ShellSleepPreventionLog.Acquired(logger, process.ProcessName);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CompleteWindowsAcquisition(long version, string mechanism)
    {
        var acquired = false;
        lock (gate)
        {
            if (isSleepPreventionPending && !isSleepPreventionActive && version == stateVersion)
            {
                isSleepPreventionActive = true;
                isSleepPreventionPending = false;
                acquired = true;
            }
        }

        if (!acquired)
        {
            ReleaseWindowsSleepPrevention();
            return;
        }

        ShellSleepPreventionLog.Acquired(logger, mechanism);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CancelPendingAcquisition(long version)
    {
        lock (gate)
        {
            if (isSleepPreventionPending && !isSleepPreventionActive && version == stateVersion)
            {
                isSleepPreventionPending = false;
            }
        }
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
