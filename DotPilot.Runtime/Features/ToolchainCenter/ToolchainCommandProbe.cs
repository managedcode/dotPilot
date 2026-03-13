using System.Diagnostics;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal static class ToolchainCommandProbe
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private const string VersionSeparator = "version";

    public static string? ResolveExecutablePath(string commandName) =>
        RuntimeFoundation.ProviderToolchainProbe.ResolveExecutablePath(commandName);

    public static string ReadVersion(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var execution = Execute(executablePath, arguments);
        if (!execution.Succeeded)
        {
            return string.Empty;
        }

        var output = string.IsNullOrWhiteSpace(execution.StandardOutput)
            ? execution.StandardError
            : execution.StandardOutput;

        var firstLine = output
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var separatorIndex = firstLine.IndexOf(VersionSeparator, StringComparison.OrdinalIgnoreCase);
        return separatorIndex >= 0
            ? firstLine[(separatorIndex + VersionSeparator.Length)..].Trim(' ', ':')
            : firstLine.Trim();
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

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return ToolchainCommandExecution.Failed;
        }

        if (!process.WaitForExit((int)CommandTimeout.TotalMilliseconds))
        {
            TryTerminate(process);
            return ToolchainCommandExecution.Failed;
        }

        return new(
            process.ExitCode == 0,
            process.StandardOutput.ReadToEnd(),
            process.StandardError.ReadToEnd());
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

    private readonly record struct ToolchainCommandExecution(bool Succeeded, string StandardOutput, string StandardError)
    {
        public static ToolchainCommandExecution Failed => new(false, string.Empty, string.Empty);
    }
}
