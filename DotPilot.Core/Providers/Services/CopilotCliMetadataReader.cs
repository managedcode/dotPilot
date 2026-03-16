using System.Diagnostics;
using System.Text.Json;
using GitHub.Copilot.SDK;

namespace DotPilot.Core.Providers;

internal static class CopilotCliMetadataReader
{
    private const string ConfigFileName = "config.json";
    private const string SuggestedModelPropertyName = "model";
    private const string EnabledPolicyState = "enabled";
    private const string ModelSettingHeader = "`model`:";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RedirectDrainTimeout = TimeSpan.FromSeconds(1);
    private const string EmptyOutput = "";

    public static async ValueTask<ProviderCliMetadataSnapshot> TryReadAsync(
        string executablePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var configuredModel = ReadConfiguredModel();
        try
        {
            return await ReadViaSdkAsync(executablePath, configuredModel, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return new ProviderCliMetadataSnapshot(
                InstalledVersion: null,
                configuredModel,
                ReadSupportedModelsFromHelp(executablePath, AgentProviderKind.GitHubCopilot.GetSupportedModelNames()));
        }
    }

    private static async ValueTask<ProviderCliMetadataSnapshot> ReadViaSdkAsync(
        string executablePath,
        string? configuredModel,
        CancellationToken cancellationToken)
    {
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            CliPath = executablePath,
            AutoStart = false,
            UseStdio = true,
        });

        await client.StartAsync(cancellationToken).ConfigureAwait(false);
        var status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);

        return new ProviderCliMetadataSnapshot(
            status.Version,
            configuredModel,
            models
                .Where(static model => string.IsNullOrWhiteSpace(model.Policy?.State) ||
                    string.Equals(model.Policy.State, EnabledPolicyState, StringComparison.OrdinalIgnoreCase))
                .Select(static model => model.Id)
                .ToArray());
    }

    private static string? ReadConfiguredModel()
    {
        var configPath = GetConfigPath();
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty(SuggestedModelPropertyName, out var property)
                ? property.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadSupportedModelsFromHelp(string executablePath, IReadOnlyList<string> fallbackModels)
    {
        var helpOutput = ReadOutput(executablePath, ["help", "config"]);
        if (string.IsNullOrWhiteSpace(helpOutput))
        {
            return fallbackModels;
        }

        List<string> models = [];
        var inModelSection = false;
        foreach (var rawLine in helpOutput.Split(Environment.NewLine, StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (!inModelSection)
            {
                inModelSection = string.Equals(line, ModelSettingHeader, StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith('`'))
            {
                break;
            }

            if (!line.StartsWith('-') &&
                !line.StartsWith('`'))
            {
                continue;
            }

            var model = line.TrimStart('-', ' ')
                .Trim('"');
            if (!string.IsNullOrWhiteSpace(model))
            {
                models.Add(model);
            }
        }

        return models.Count == 0 ? fallbackModels : models;
    }

    private static string GetConfigPath()
    {
        return ProviderCliHomeDirectory.GetFilePath(".copilot", ConfigFileName);
    }

    private static string ReadOutput(string executablePath, IReadOnlyList<string> arguments)
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

                return new ToolchainCommandExecution(
                    true,
                    false,
                    AwaitStreamRead(standardOutputTask),
                    AwaitStreamRead(standardErrorTask));
            }

            return new ToolchainCommandExecution(
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
