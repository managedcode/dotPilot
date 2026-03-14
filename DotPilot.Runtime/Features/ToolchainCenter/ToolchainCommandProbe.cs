using System.Diagnostics;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal static class ToolchainCommandProbe
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RedirectDrainTimeout = TimeSpan.FromSeconds(1);
    private const string VersionSeparator = "version";
    private const string EmptyOutput = "";

    public static string? ResolveExecutablePath(string commandName) =>
        RuntimeFoundation.ProviderToolchainProbe.ResolveExecutablePath(commandName);

    public static string ReadVersion(string executablePath, IReadOnlyList<string> arguments)
        => ProbeVersion(executablePath, arguments).Version;

    public static ToolchainVersionProbeResult ProbeVersion(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var execution = Execute(executablePath, arguments);
        if (!execution.Succeeded)
        {
            return ToolchainVersionProbeResult.Missing with { Launched = execution.Launched };
        }

        var output = string.IsNullOrWhiteSpace(execution.StandardOutput)
            ? execution.StandardError
            : execution.StandardOutput;

        var firstLine = output
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return ToolchainVersionProbeResult.Missing with { Launched = execution.Launched };
        }

        var separatorIndex = firstLine.IndexOf(VersionSeparator, StringComparison.OrdinalIgnoreCase);
        var version = separatorIndex >= 0
            ? firstLine[(separatorIndex + VersionSeparator.Length)..].Trim(' ', ':')
            : firstLine.Trim();

        return new(execution.Launched, version);
    }

    public static bool CanExecute(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        return Execute(executablePath, arguments).Succeeded;
    }

    private static ToolchainCommandExecution Execute(string executablePath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch
        {
            return ToolchainCommandExecution.LaunchFailed;
        }

        if (process is null)
        {
            return ToolchainCommandExecution.LaunchFailed;
        }

        using (process)
        {
            var standardOutputTask = ObserveRedirectedStream(process.StandardOutput.ReadToEndAsync());
            var standardErrorTask = ObserveRedirectedStream(process.StandardError.ReadToEndAsync());

            if (!process.WaitForExit((int)CommandTimeout.TotalMilliseconds))
            {
                TryTerminate(process);
                WaitForTermination(process);

                return new(
                    true,
                    false,
                    AwaitStreamRead(standardOutputTask),
                    AwaitStreamRead(standardErrorTask));
            }

            return new(
                true,
                process.ExitCode == 0,
                AwaitStreamRead(standardOutputTask),
                AwaitStreamRead(standardErrorTask));
        }
    }

    private static Task<string> ObserveRedirectedStream(Task<string> readTask)
    {
        _ = readTask.ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return readTask;
    }

    private static string AwaitStreamRead(Task<string> readTask)
    {
        try
        {
            if (!readTask.Wait(RedirectDrainTimeout))
            {
                return EmptyOutput;
            }

            return readTask.GetAwaiter().GetResult();
        }
        catch
        {
            return EmptyOutput;
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static void WaitForTermination(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.WaitForExit((int)RedirectDrainTimeout.TotalMilliseconds);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    public readonly record struct ToolchainVersionProbeResult(bool Launched, string Version)
    {
        public static ToolchainVersionProbeResult Missing => new(false, EmptyOutput);
    }

    private readonly record struct ToolchainCommandExecution(bool Launched, bool Succeeded, string StandardOutput, string StandardError)
    {
        public static ToolchainCommandExecution LaunchFailed => new(false, false, EmptyOutput, EmptyOutput);
    }
}
