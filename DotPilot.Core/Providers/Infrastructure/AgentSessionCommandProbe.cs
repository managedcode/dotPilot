using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotPilot.Core.Providers;

internal static class AgentSessionCommandProbe
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RedirectDrainTimeout = TimeSpan.FromSeconds(1);
    private const string VersionSeparator = "version";
    private const string EmptyOutput = "";

    public static string? ResolveExecutablePath(string commandName)
    {
        if (OperatingSystem.IsBrowser())
        {
            return null;
        }

        var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var searchPath in searchPaths)
        {
            foreach (var candidate in EnumerateCandidates(searchPath, commandName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static string ReadVersion(string executablePath, IReadOnlyList<string> arguments)
    {
        var output = ReadOutput(executablePath, arguments);
        var firstLine = output
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return EmptyOutput;
        }

        var separatorIndex = firstLine.IndexOf(VersionSeparator, StringComparison.OrdinalIgnoreCase);
        return separatorIndex >= 0
            ? firstLine[(separatorIndex + VersionSeparator.Length)..].Trim(' ', ':')
            : firstLine.Trim();
    }

    public static string ReadOutput(string executablePath, IReadOnlyList<string> arguments)
    {
        var execution = Execute(executablePath, arguments);
        if (!execution.Succeeded)
        {
            return EmptyOutput;
        }

        return string.IsNullOrWhiteSpace(execution.StandardOutput)
            ? execution.StandardError
            : execution.StandardOutput;
    }

    private static ToolchainCommandExecution Execute(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

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

    private static IEnumerable<string> EnumerateCandidates(string searchPath, string commandName)
    {
        yield return Path.Combine(searchPath, commandName);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield break;
        }

        foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(searchPath, string.Concat(commandName, extension));
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
        }
    }

    private readonly record struct ToolchainCommandExecution(bool Launched, bool Succeeded, string StandardOutput, string StandardError)
    {
        public static ToolchainCommandExecution LaunchFailed => new(false, false, EmptyOutput, EmptyOutput);
    }
}
