using System.Diagnostics;

namespace DotPilot.UITests;

internal static class BrowserTestHost
{
    private const string DotnetExecutableName = "dotnet";
    private const string RunCommand = "run";
    private const string ConfigurationOption = "-c";
    private const string ReleaseConfiguration = "Release";
    private const string FrameworkOption = "-f";
    private const string BrowserFramework = "net10.0-browserwasm";
    private const string ProjectOption = "--project";
    private const string NoLaunchProfileOption = "--no-launch-profile";
    private const string UiAutomationProperty = "-p:IsUiAutomationMappingEnabled=True";
    private const string ProjectRelativePath = "DotPilot/DotPilot.csproj";
    private const string SolutionMarkerFileName = "DotPilot.slnx";
    private const string HostReadyTimeoutMessage = "Timed out waiting for the WebAssembly host to become reachable.";
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
            var projectDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidOperationException($"Could not resolve project directory for '{projectPath}'.");

            HarnessLog.Write("Starting browser host process.");
            StartHostProcess(projectDirectory, projectPath);
            WaitForHost(hostUri);
        }
    }

    private static void StartHostProcess(string projectDirectory, string projectPath)
    {
        var processStartInfo = CreateStartInfo(projectDirectory);
        foreach (var argument in CreateRunArguments(projectPath))
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        processStartInfo.Environment["ASPNETCORE_URLS"] = BrowserTestEnvironment.WebAssemblyUrlsValue;

        _hostProcess = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start the WebAssembly test host.");
        _hostProcess.OutputDataReceived += (_, args) => CaptureOutput(args.Data);
        _hostProcess.ErrorDataReceived += (_, args) => CaptureOutput(args.Data);
        _hostProcess.BeginOutputReadLine();
        _hostProcess.BeginErrorReadLine();
        _startedHost = true;
        HarnessLog.Write($"Browser host process started with PID {_hostProcess.Id}.");
    }

    internal static IReadOnlyList<string> CreateRunArguments(string projectPath)
    {
        return
        [
            RunCommand,
            ConfigurationOption,
            ReleaseConfiguration,
            FrameworkOption,
            BrowserFramework,
            UiAutomationProperty,
            ProjectOption,
            projectPath,
            NoLaunchProfileOption,
        ];
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

    private static ProcessStartInfo CreateStartInfo(string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = DotnetExecutableName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
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
