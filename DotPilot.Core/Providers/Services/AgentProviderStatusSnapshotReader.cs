using System.Diagnostics;
using System.Runtime.InteropServices;
using DotPilot.Core.ChatSessions;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Core.Providers;

internal static class AgentProviderStatusSnapshotReader
{
    private const string BrowserStatusSummary =
        "Desktop CLI probing is unavailable in the browser automation head. Enable the provider to author its profile here.";
    private const string DisabledStatusSummary = "Provider is disabled for local agent creation.";
    private const string BuiltInStatusSummary = "Built in and ready for deterministic local testing.";
    private const string MissingCliSummaryFormat = "{0} CLI is not installed.";
    private const string ReadySummaryFormat = "{0} CLI is ready for local desktop execution.";
    private const string TimedOutSummaryFormat = "{0} CLI probe timed out. Refresh status to retry.";
    private const string ModelPathVariablesLabel = "Model path variables";
    private const string ConfiguredModelPathLabel = "Configured model path";
    private const string OpenCliActionLabel = "Open CLI";
    private const string OpenCliActionSummary = "CLI detected on PATH.";
    private const string InstallActionLabel = "Install";
    private const string InstallActionSummary = "Install the CLI, then refresh settings.";
    private const string TimedOutActionSummary = "CLI detected on PATH, but the readiness probe timed out.";
    private static readonly TimeSpan ProviderProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RedirectDrainTimeout = TimeSpan.FromSeconds(1);
    private const string VersionSeparator = "version";
    private const string EmptyOutput = "";
    private static readonly System.Text.CompositeFormat MissingCliSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(MissingCliSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadySummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(ReadySummaryFormat);
    private static readonly System.Text.CompositeFormat TimedOutSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(TimedOutSummaryFormat);
    private static readonly IReadOnlyList<ProviderLocalModelRecord> EmptyLocalModelRecords = Array.Empty<ProviderLocalModelRecord>();

    public static async Task<IReadOnlyList<ProviderStatusProbeResult>> BuildAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var preferences = await dbContext.ProviderPreferences
            .ToDictionaryAsync(
                preference => (AgentProviderKind)preference.ProviderKind,
                cancellationToken);
        var localModelsByProvider = (await dbContext.ProviderLocalModels
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(record => (AgentProviderKind)record.ProviderKind)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProviderLocalModelRecord>)group.ToArray());
        var providerKinds = Enum.GetValues<AgentProviderKind>();
        var probeTasks = providerKinds
            .Select(providerKind => ProbeProviderAsync(
                providerKind,
                GetProviderPreference(providerKind, preferences),
                GetLocalModels(providerKind, localModelsByProvider),
                cancellationToken))
            .ToArray();
        var results = await Task.WhenAll(probeTasks)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return results;
    }

    private static ProviderPreferenceRecord GetProviderPreference(
        AgentProviderKind kind,
        Dictionary<AgentProviderKind, ProviderPreferenceRecord> preferences)
    {
        return preferences.TryGetValue(kind, out var preference)
            ? preference
            : new ProviderPreferenceRecord
            {
                ProviderKind = (int)kind,
                IsEnabled = false,
                LocalModelPath = null,
                UpdatedAt = DateTimeOffset.MinValue,
            };
    }

    private static IReadOnlyList<ProviderLocalModelRecord> GetLocalModels(
        AgentProviderKind kind,
        Dictionary<AgentProviderKind, IReadOnlyList<ProviderLocalModelRecord>> localModelsByProvider)
    {
        return localModelsByProvider.TryGetValue(kind, out var localModels)
            ? localModels
            : EmptyLocalModelRecords;
    }

    private static async ValueTask<ProviderStatusProbeResult> BuildProviderStatusAsync(
        AgentProviderKind providerKind,
        ProviderPreferenceRecord preference,
        IReadOnlyList<ProviderLocalModelRecord> localModels,
        CancellationToken cancellationToken)
    {
        var isBuiltIn = providerKind.IsBuiltIn();
        var commandName = providerKind.GetCommandName();
        var displayName = providerKind.GetDisplayName();
        var defaultModelName = isBuiltIn
            ? providerKind.GetDefaultModelName()
            : string.Empty;
        var installCommand = providerKind.GetInstallCommand();
        var fallbackModels = isBuiltIn ? providerKind.GetSupportedModelNames() : [];
        var providerId = AgentSessionDeterministicIdentity.CreateProviderId(commandName);
        var actions = new List<ProviderActionDescriptor>();
        var details = new List<ProviderDetailDescriptor>();
        string? executablePath = null;
        var installedVersion = isBuiltIn ? defaultModelName : (string?)null;
        var suggestedModelName = defaultModelName;
        var supportedModelNames = ResolveSupportedModels(
            defaultModelName,
            defaultModelName,
            fallbackModels,
            []);
        var status = AgentProviderStatus.Ready;
        var statusSummary = BuiltInStatusSummary;
        var canCreateAgents = isBuiltIn;

        if (OperatingSystem.IsBrowser() && !isBuiltIn)
        {
            details.Add(new ProviderDetailDescriptor("Install command", installCommand));
            actions.Add(new ProviderActionDescriptor("Install", "Run this on desktop.", installCommand, ProviderActionKind.CopyCommand));
            status = AgentProviderStatus.Unsupported;
            statusSummary = BrowserStatusSummary;
            canCreateAgents = preference.IsEnabled;
        }
        else if (providerKind.IsLocalModelProvider())
        {
            var configuration = await LocalModelProviderConfigurationReader.ReadAsync(
                providerKind,
                localModels,
                preference.LocalModelPath,
                cancellationToken).ConfigureAwait(false);
            details.Add(new ProviderDetailDescriptor(
                ModelPathVariablesLabel,
                string.Join(", ", configuration.EnvironmentVariableNames)));
            actions.Add(new ProviderActionDescriptor(
                providerKind.GetLocalModelPickerLabel(),
                providerKind.GetLocalModelSetupSummary(),
                string.Empty,
                providerKind.GetLocalModelPickerActionKind()));

            var configuredModelPaths = FormatDetailValues(configuration.ConfiguredModelPaths);
            if (!string.IsNullOrWhiteSpace(configuredModelPaths))
            {
                details.Add(new ProviderDetailDescriptor(
                    configuration.ConfiguredModelPaths.Count > 1
                        ? "Configured model paths"
                        : ConfiguredModelPathLabel,
                    configuredModelPaths));
            }

            var detectedRuntimeTypes = FormatDetailValues(configuration.DetectedRuntimeTypes);
            if (!string.IsNullOrWhiteSpace(detectedRuntimeTypes))
            {
                details.Add(new ProviderDetailDescriptor(
                    providerKind.GetLocalModelDetectedRuntimeTypeLabel(),
                    detectedRuntimeTypes));
            }

            var supportedRuntimeTypes = FormatSupportedModels(configuration.SupportedRuntimeTypes);
            if (!string.IsNullOrWhiteSpace(supportedRuntimeTypes))
            {
                details.Add(new ProviderDetailDescriptor(
                    providerKind.GetLocalModelSupportedRuntimeTypesLabel(),
                    supportedRuntimeTypes));
            }

            if (!configuration.IsReady)
            {
                suggestedModelName = configuration.SuggestedModelName ?? string.Empty;
                supportedModelNames = configuration.SupportedModelNames;
                status = string.IsNullOrWhiteSpace(configuration.ModelPath)
                    ? AgentProviderStatus.RequiresSetup
                    : AgentProviderStatus.Error;
                statusSummary = string.IsNullOrWhiteSpace(configuration.ValidationErrorMessage)
                    ? providerKind.GetLocalModelMissingSummary()
                    : configuration.ValidationErrorMessage;
                canCreateAgents = false;
            }
            else
            {
                suggestedModelName = ResolveSuggestedModel(
                    defaultModelName,
                    configuration.SuggestedModelName,
                    configuration.SupportedModelNames);
                supportedModelNames = configuration.SupportedModelNames;
                details.AddRange(CreateProviderDetails(installedVersion, suggestedModelName, supportedModelNames));
                statusSummary = providerKind.GetLocalModelReadySummary();
                canCreateAgents = true;
            }
        }
        else if (!isBuiltIn)
        {
            executablePath = ResolveExecutablePath(commandName);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                details.Add(new ProviderDetailDescriptor("Install command", installCommand));
                actions.Add(new ProviderActionDescriptor(InstallActionLabel, InstallActionSummary, installCommand, ProviderActionKind.CopyCommand));
                status = AgentProviderStatus.RequiresSetup;
                statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, MissingCliSummaryCompositeFormat, displayName);
                canCreateAgents = false;
            }
            else
            {
                var metadata = await ResolveMetadataAsync(providerKind, executablePath, cancellationToken).ConfigureAwait(false);
                installedVersion = metadata.InstalledVersion;
                if (!LooksLikeInstalledVersion(installedVersion, commandName))
                {
                    installedVersion = ReadVersion(executablePath, ["--version"]);
                }

                actions.Add(new ProviderActionDescriptor(OpenCliActionLabel, OpenCliActionSummary, $"{commandName} --version", ProviderActionKind.CopyCommand));
                suggestedModelName = ResolveSuggestedModel(
                    defaultModelName,
                    metadata.SuggestedModelName,
                    metadata.SupportedModels);
                supportedModelNames = ResolveSupportedModels(
                    defaultModelName,
                    suggestedModelName,
                    fallbackModels,
                    metadata.SupportedModels);
                details.AddRange(CreateProviderDetails(installedVersion, suggestedModelName, supportedModelNames));
                statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadySummaryCompositeFormat, displayName);
                canCreateAgents = true;
            }
        }

        if (!preference.IsEnabled)
        {
            status = AgentProviderStatus.Disabled;
            statusSummary = $"{DisabledStatusSummary} {statusSummary}";
            canCreateAgents = false;
        }

        return new ProviderStatusProbeResult(
            new ProviderStatusDescriptor(
                providerId,
                providerKind,
                displayName,
                commandName,
                status,
                statusSummary,
                suggestedModelName,
                supportedModelNames,
                installedVersion,
                preference.IsEnabled,
                canCreateAgents,
                details,
                actions),
            executablePath);
    }

    private static async Task<ProviderStatusProbeResult> ProbeProviderAsync(
        AgentProviderKind providerKind,
        ProviderPreferenceRecord preference,
        IReadOnlyList<ProviderLocalModelRecord> localModels,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(
                    async () => await BuildProviderStatusAsync(providerKind, preference, localModels, cancellationToken).ConfigureAwait(false),
                    CancellationToken.None)
                .WaitAsync(ProviderProbeTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return CreateTimedOutProviderStatus(providerKind, preference);
        }
    }

    private static ProviderStatusProbeResult CreateTimedOutProviderStatus(
        AgentProviderKind providerKind,
        ProviderPreferenceRecord preference)
    {
        var commandName = providerKind.GetCommandName();
        var displayName = providerKind.GetDisplayName();
        var suggestedModelName = providerKind.GetDefaultModelName();
        IReadOnlyList<string> supportedModelNames = string.IsNullOrWhiteSpace(suggestedModelName)
            ? Array.Empty<string>()
            : [suggestedModelName];
        var executablePath = ResolveExecutablePath(commandName);
        var actions = new List<ProviderActionDescriptor>();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            actions.Add(new ProviderActionDescriptor(
                InstallActionLabel,
                InstallActionSummary,
                providerKind.GetInstallCommand(),
                ProviderActionKind.CopyCommand));
        }
        else
        {
            actions.Add(new ProviderActionDescriptor(
                OpenCliActionLabel,
                TimedOutActionSummary,
                $"{commandName} --version",
                ProviderActionKind.CopyCommand));
        }

        var details = CreateProviderDetails(
            installedVersion: null,
            suggestedModelName,
            supportedModelNames);
        var status = AgentProviderStatus.Error;
        var statusSummary = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            TimedOutSummaryCompositeFormat,
            displayName);
        var canCreateAgents = false;
        if (!preference.IsEnabled)
        {
            status = AgentProviderStatus.Disabled;
            statusSummary = $"{DisabledStatusSummary} {statusSummary}";
        }

        return new ProviderStatusProbeResult(
            new ProviderStatusDescriptor(
                AgentSessionDeterministicIdentity.CreateProviderId(commandName),
                providerKind,
                displayName,
                commandName,
                status,
                statusSummary,
                suggestedModelName,
                supportedModelNames,
                null,
                preference.IsEnabled,
                canCreateAgents,
                details,
                actions),
            executablePath);
    }

    private static async ValueTask<ProviderCliMetadataSnapshot> ResolveMetadataAsync(
        AgentProviderKind providerKind,
        string executablePath,
        CancellationToken cancellationToken)
    {
        return providerKind switch
        {
            AgentProviderKind.Codex => CreateCodexSnapshot(CodexCliMetadataReader.TryRead(executablePath)),
            AgentProviderKind.ClaudeCode => ClaudeCodeCliMetadataReader.TryRead(executablePath),
            AgentProviderKind.GitHubCopilot => await CopilotCliMetadataReader.TryReadAsync(
                executablePath,
                cancellationToken).ConfigureAwait(false),
            AgentProviderKind.Gemini => GeminiCliMetadataReader.TryRead(executablePath),
            _ => new ProviderCliMetadataSnapshot(null, null, []),
        };
    }

    private static ProviderCliMetadataSnapshot CreateCodexSnapshot(CodexCliMetadataSnapshot? metadata)
    {
        return new ProviderCliMetadataSnapshot(
            metadata?.InstalledVersion,
            metadata?.DefaultModel,
            metadata?.AvailableModels ?? []);
    }

    private static string ResolveSuggestedModel(
        string defaultModelName,
        string? suggestedModelName,
        IReadOnlyList<string> discoveredModels)
    {
        if (!string.IsNullOrWhiteSpace(suggestedModelName))
        {
            return suggestedModelName;
        }

        var discoveredModel = discoveredModels.FirstOrDefault(static model => !string.IsNullOrWhiteSpace(model));
        return string.IsNullOrWhiteSpace(discoveredModel)
            ? defaultModelName
            : discoveredModel;
    }

    private static bool LooksLikeInstalledVersion(string? installedVersion, string commandName)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return false;
        }

        if (string.Equals(installedVersion, commandName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return installedVersion.Any(char.IsDigit);
    }

    private static IReadOnlyList<string> ResolveSupportedModels(
        string defaultModelName,
        string suggestedModelName,
        IReadOnlyList<string> fallbackModels,
        IReadOnlyList<string> discoveredModels)
    {
        return [.. EnumerateSupportedModels(defaultModelName, suggestedModelName, fallbackModels, discoveredModels)];
    }

    private static IEnumerable<string> EnumerateSupportedModels(
        string defaultModelName,
        string suggestedModelName,
        IReadOnlyList<string> fallbackModels,
        IReadOnlyList<string> discoveredModels)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var model in new[] { suggestedModelName, defaultModelName }
                     .Concat(discoveredModels)
                     .Concat(fallbackModels))
        {
            if (string.IsNullOrWhiteSpace(model) || !seen.Add(model))
            {
                continue;
            }

            yield return model;
        }
    }

    private static List<ProviderDetailDescriptor> CreateProviderDetails(
        string? installedVersion,
        string suggestedModelName,
        IReadOnlyList<string> supportedModelNames)
    {
        List<ProviderDetailDescriptor> details = [];
        if (!string.IsNullOrWhiteSpace(installedVersion))
        {
            details.Add(new ProviderDetailDescriptor("Installed version", installedVersion));
        }

        if (!string.IsNullOrWhiteSpace(suggestedModelName))
        {
            details.Add(new ProviderDetailDescriptor("Suggested model", suggestedModelName));
        }

        var supportedModels = FormatSupportedModels(supportedModelNames);
        if (!string.IsNullOrWhiteSpace(supportedModels))
        {
            details.Add(new ProviderDetailDescriptor("Supported models", supportedModels));
        }

        return details;
    }

    private static string FormatSupportedModels(IReadOnlyList<string> models)
    {
        if (models.Count == 0)
        {
            return string.Empty;
        }

        const int limit = 8;
        var visibleModels = models
            .Where(static model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        if (visibleModels.Length == 0)
        {
            return string.Empty;
        }

        var remaining = models
            .Where(static model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.Ordinal)
            .Count() - visibleModels.Length;
        var summary = string.Join(", ", visibleModels);
        return remaining > 0
            ? $"{summary} (+{remaining} more)"
            : summary;
    }

    private static string FormatDetailValues(IReadOnlyList<string> values)
    {
        return string.Join(
            Environment.NewLine,
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string? ResolveExecutablePath(string commandName)
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

    private static string ReadVersion(string executablePath, IReadOnlyList<string> arguments)
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
