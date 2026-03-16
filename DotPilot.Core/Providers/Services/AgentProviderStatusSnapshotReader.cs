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
    private const string ProfileAuthoringAvailableSummaryFormat =
        "{0} profile authoring is available, but live desktop execution is not available in this app yet.";
    private static readonly System.Text.CompositeFormat MissingCliSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(MissingCliSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadySummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(ReadySummaryFormat);
    private static readonly System.Text.CompositeFormat ProfileAuthoringAvailableCompositeFormat =
        System.Text.CompositeFormat.Parse(ProfileAuthoringAvailableSummaryFormat);

    public static async Task<IReadOnlyList<ProviderStatusProbeResult>> BuildAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var preferences = await dbContext.ProviderPreferences
            .ToDictionaryAsync(
                preference => (AgentProviderKind)preference.ProviderKind,
                cancellationToken);
        var profiles = AgentSessionProviderCatalog.All;

        return await Task.Run(
            async () =>
            {
                List<ProviderStatusProbeResult> results = new(profiles.Count);
                foreach (var profile in profiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(await BuildProviderStatusAsync(
                        profile,
                        GetProviderPreference(profile.Kind, preferences),
                        cancellationToken).ConfigureAwait(false));
                }

                return (IReadOnlyList<ProviderStatusProbeResult>)results;
            },
            cancellationToken).ConfigureAwait(false);
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
                UpdatedAt = DateTimeOffset.MinValue,
            };
    }

    private static async ValueTask<ProviderStatusProbeResult> BuildProviderStatusAsync(
        AgentSessionProviderProfile profile,
        ProviderPreferenceRecord preference,
        CancellationToken cancellationToken)
    {
        var providerId = AgentSessionDeterministicIdentity.CreateProviderId(profile.CommandName);
        var actions = new List<ProviderActionDescriptor>();
        var details = new List<ProviderDetailDescriptor>();
        string? executablePath = null;
        var installedVersion = profile.IsBuiltIn ? profile.DefaultModelName : (string?)null;
        var suggestedModelName = profile.DefaultModelName;
        var supportedModelNames = ResolveSupportedModels(
            profile.DefaultModelName,
            profile.DefaultModelName,
            profile.SupportedModelNames,
            []);
        var status = AgentProviderStatus.Ready;
        var statusSummary = BuiltInStatusSummary;
        var canCreateAgents = profile.IsBuiltIn;

        if (OperatingSystem.IsBrowser() && !profile.IsBuiltIn)
        {
            details.Add(new ProviderDetailDescriptor("Install command", profile.InstallCommand));
            actions.Add(new ProviderActionDescriptor("Install", "Run this on desktop.", profile.InstallCommand));
            status = AgentProviderStatus.Unsupported;
            statusSummary = BrowserStatusSummary;
            canCreateAgents = preference.IsEnabled;
        }
        else if (!profile.IsBuiltIn)
        {
            executablePath = AgentSessionCommandProbe.ResolveExecutablePath(profile.CommandName);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                details.Add(new ProviderDetailDescriptor("Install command", profile.InstallCommand));
                actions.Add(new ProviderActionDescriptor("Install", "Install the CLI, then refresh settings.", profile.InstallCommand));
                status = AgentProviderStatus.RequiresSetup;
                statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, MissingCliSummaryCompositeFormat, profile.DisplayName);
                canCreateAgents = false;
            }
            else
            {
                var metadata = await ResolveMetadataAsync(profile, executablePath, cancellationToken).ConfigureAwait(false);
                installedVersion = metadata.InstalledVersion;
                if (!LooksLikeInstalledVersion(installedVersion, profile.CommandName))
                {
                    installedVersion = AgentSessionCommandProbe.ReadVersion(executablePath, ["--version"]);
                }

                actions.Add(new ProviderActionDescriptor("Open CLI", "CLI detected on PATH.", $"{profile.CommandName} --version"));
                suggestedModelName = ResolveSuggestedModel(profile.DefaultModelName, metadata.SuggestedModelName);
                supportedModelNames = ResolveSupportedModels(
                    profile.DefaultModelName,
                    suggestedModelName,
                    profile.SupportedModelNames,
                    metadata.SupportedModels);
                details.AddRange(CreateProviderDetails(installedVersion, suggestedModelName, supportedModelNames));

                if (profile.SupportsLiveExecution)
                {
                    statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadySummaryCompositeFormat, profile.DisplayName);
                    canCreateAgents = true;
                }
                else
                {
                    status = AgentProviderStatus.Unsupported;
                    statusSummary = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        ProfileAuthoringAvailableCompositeFormat,
                        profile.DisplayName);
                    canCreateAgents = true;
                }
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
                profile.Kind,
                profile.DisplayName,
                profile.CommandName,
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

    private static async ValueTask<ProviderCliMetadataSnapshot> ResolveMetadataAsync(
        AgentSessionProviderProfile profile,
        string executablePath,
        CancellationToken cancellationToken)
    {
        return profile.Kind switch
        {
            AgentProviderKind.Codex => CreateCodexSnapshot(CodexCliMetadataReader.TryRead(executablePath)),
            AgentProviderKind.ClaudeCode => ClaudeCodeCliMetadataReader.TryRead(executablePath, profile),
            AgentProviderKind.GitHubCopilot => await CopilotCliMetadataReader.TryReadAsync(
                executablePath,
                profile,
                cancellationToken).ConfigureAwait(false),
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

    private static string ResolveSuggestedModel(string defaultModelName, string? suggestedModelName)
    {
        return string.IsNullOrWhiteSpace(suggestedModelName)
            ? defaultModelName
            : suggestedModelName;
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

        details.Add(new ProviderDetailDescriptor("Suggested model", suggestedModelName));

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
}
