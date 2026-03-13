using System.Diagnostics;

namespace DotPilot.UITests;

internal static class BrowserTestHost
{
    private const string DotnetExecutableName = "dotnet";
    private const string BuildCommand = "build";
    private const string RunCommand = "run";
    private const string ConfigurationOption = "-c";
    private const string ReleaseConfiguration = "Release";
    private const string FrameworkOption = "-f";
    private const string BrowserFramework = "net10.0-browserwasm";
    private const string ProjectOption = "--project";
    private const string NoBuildOption = "--no-build";
    private const string NoLaunchProfileOption = "--no-launch-profile";
    private const string UiAutomationProperty = "-p:IsUiAutomationMappingEnabled=True";
    private const string ProjectRelativePath = "DotPilot/DotPilot.csproj";
    private const string SolutionMarkerFileName = "DotPilot.slnx";
    private const string HostReadyTimeoutMessage = "Timed out waiting for the WebAssembly host to become reachable.";
    private const string BuildFailureMessage = "Failed to build the WebAssembly test host.";
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan HostStartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HostShutdownTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan HostProbeInterval = TimeSpan.FromMilliseconds(250);
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2),
    };

    private static readonly object SyncRoot = new();
    private static Process? _hostProcess;
    private static bool _startedHost;
    private static string _lastOutput = string.Empty;

    static BrowserTestHost()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
    }

    public static void EnsureStarted(string hostUri)
    {
        lock (SyncRoot)
        {
            if (IsReachable(hostUri))
            {
                HarnessLog.Write("Browser host is already reachable.");
                return;
            }

            if (_hostProcess is { HasExited: false })
            {
                HarnessLog.Write("Browser host process already exists. Waiting for readiness.");
                WaitForHost(hostUri);
                return;
            }

            var repoRoot = FindRepositoryRoot();
            var projectPath = Path.Combine(repoRoot, ProjectRelativePath);

            HarnessLog.Write("Building browser host.");
            EnsureBuilt(repoRoot, projectPath);
            HarnessLog.Write("Starting browser host process.");
            StartHostProcess(repoRoot, projectPath);
            WaitForHost(hostUri);
        }
    }

    private static void EnsureBuilt(string repoRoot, string projectPath)
    {
        var buildStartInfo = CreateStartInfo(repoRoot);
        buildStartInfo.ArgumentList.Add(BuildCommand);
        buildStartInfo.ArgumentList.Add(ConfigurationOption);
        buildStartInfo.ArgumentList.Add(ReleaseConfiguration);
        buildStartInfo.ArgumentList.Add(FrameworkOption);
        buildStartInfo.ArgumentList.Add(BrowserFramework);
        buildStartInfo.ArgumentList.Add(UiAutomationProperty);
        buildStartInfo.ArgumentList.Add(projectPath);

        var result = RunAndCapture(buildStartInfo, BuildTimeout);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{BuildFailureMessage} {result.Output}");
        }

        HarnessLog.Write("Browser host build completed.");
    }

    private static void StartHostProcess(string repoRoot, string projectPath)
    {
        var processStartInfo = CreateStartInfo(repoRoot);
        processStartInfo.ArgumentList.Add(RunCommand);
        processStartInfo.ArgumentList.Add(ConfigurationOption);
        processStartInfo.ArgumentList.Add(ReleaseConfiguration);
        processStartInfo.ArgumentList.Add(FrameworkOption);
        processStartInfo.ArgumentList.Add(BrowserFramework);
        processStartInfo.ArgumentList.Add(UiAutomationProperty);
        processStartInfo.ArgumentList.Add(ProjectOption);
        processStartInfo.ArgumentList.Add(projectPath);
        processStartInfo.ArgumentList.Add(NoBuildOption);
        processStartInfo.ArgumentList.Add(NoLaunchProfileOption);
        processStartInfo.Environment["ASPNETCORE_URLS"] = BrowserTestEnvironment.WebAssemblyUrlsValue;

        _hostProcess = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start the WebAssembly test host.");
        _hostProcess.OutputDataReceived += (_, args) => CaptureOutput(args.Data);
        _hostProcess.ErrorDataReceived += (_, args) => CaptureOutput(args.Data);
        _hostProcess.BeginOutputReadLine();
        _hostProcess.BeginErrorReadLine();
        _startedHost = true;
        HarnessLog.Write($"Browser host process started with PID {_hostProcess.Id}.");
    }

    private static void CaptureOutput(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            _lastOutput = line;
        }
    }

    private static void WaitForHost(string hostUri)
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(HostStartupTimeout);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (IsReachable(hostUri))
            {
                HarnessLog.Write("Browser host responded to readiness probe.");
                return;
            }

            if (_hostProcess is { HasExited: true } exitedProcess)
            {
                throw new InvalidOperationException(
                    $"The WebAssembly host exited before it became reachable. Exit code: {exitedProcess.ExitCode}. Last output: {_lastOutput}");
            }

            Task.Delay(HostProbeInterval).GetAwaiter().GetResult();
        }

        throw new InvalidOperationException($"{HostReadyTimeoutMessage} Last output: {_lastOutput}");
    }

    private static bool IsReachable(string hostUri)
    {
        try
        {
            using var response = HttpClient.GetAsync(hostUri).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessStartInfo CreateStartInfo(string repoRoot)
    {
        return new ProcessStartInfo
        {
            FileName = DotnetExecutableName,
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static (int ExitCode, string Output) RunAndCapture(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup only.
            }

            throw new TimeoutException($"Timed out waiting for dotnet process to finish within {timeout}.");
        }

        var combinedOutput =
            $"{standardOutputTask.GetAwaiter().GetResult()}{Environment.NewLine}{standardErrorTask.GetAwaiter().GetResult()}";

        return (process.ExitCode, combinedOutput.Trim());
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var markerPath = Path.Combine(currentDirectory.FullName, SolutionMarkerFileName);
            if (File.Exists(markerPath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not locate repository root containing {SolutionMarkerFileName}.");
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            if (!_startedHost || _hostProcess is null)
            {
                HarnessLog.Write("Browser host stop requested, but no owned host process is active.");
                return;
            }

            var hostProcess = _hostProcess;
            _hostProcess = null;
            _startedHost = false;
            _lastOutput = string.Empty;

            try
            {
                HarnessLog.Write($"Stopping browser host process {hostProcess.Id}.");
                CancelOutputReaders(hostProcess);

                if (!hostProcess.HasExited)
                {
                    hostProcess.Kill(entireProcessTree: true);
                    hostProcess.WaitForExit((int)HostShutdownTimeout.TotalMilliseconds);
                }

                HarnessLog.Write("Browser host process stopped.");
            }
            catch
            {
                // Best-effort cleanup only.
            }
            finally
            {
                hostProcess.Dispose();
            }
        }
    }

    private static void CancelOutputReaders(Process process)
    {
        try
        {
            process.CancelOutputRead();
        }
        catch
        {
            // Best-effort cleanup only.
        }

        try
        {
            process.CancelErrorRead();
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}
