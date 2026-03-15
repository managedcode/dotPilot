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

        return await Task.Run(
            () => AgentSessionProviderCatalog.All
                .Select(profile => BuildProviderStatus(profile, GetProviderPreference(profile.Kind, preferences)))
                .ToArray(),
            cancellationToken);
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

    private static ProviderStatusProbeResult BuildProviderStatus(
        AgentSessionProviderProfile profile,
        ProviderPreferenceRecord preference)
    {
        var providerId = AgentSessionDeterministicIdentity.CreateProviderId(profile.CommandName);
        var actions = new List<ProviderActionDescriptor>();
        var details = new List<ProviderDetailDescriptor>();
        string? executablePath = null;
        var installedVersion = profile.IsBuiltIn ? profile.DefaultModelName : (string?)null;
        var suggestedModelName = profile.DefaultModelName;
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
                installedVersion = AgentSessionCommandProbe.ReadVersion(executablePath, ["--version"]);
                actions.Add(new ProviderActionDescriptor("Open CLI", "CLI detected on PATH.", $"{profile.CommandName} --version"));

                if (profile.Kind == AgentProviderKind.Codex)
                {
                    var codexMetadata = CodexCliMetadataReader.TryRead(executablePath);
                    installedVersion = codexMetadata?.InstalledVersion ?? installedVersion;
                    suggestedModelName = string.IsNullOrWhiteSpace(codexMetadata?.DefaultModel)
                        ? suggestedModelName
                        : codexMetadata.DefaultModel!;
                    details.AddRange(CreateCodexDetails(installedVersion, suggestedModelName, codexMetadata));
                }
                else if (!string.IsNullOrWhiteSpace(installedVersion))
                {
                    details.Add(new ProviderDetailDescriptor("Installed version", installedVersion));
                }

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
                installedVersion,
                preference.IsEnabled,
                canCreateAgents,
                details,
                actions),
            executablePath);
    }

    private static List<ProviderDetailDescriptor> CreateCodexDetails(
        string? installedVersion,
        string suggestedModelName,
        CodexCliMetadataSnapshot? metadata)
    {
        List<ProviderDetailDescriptor> details = [];
        if (!string.IsNullOrWhiteSpace(installedVersion))
        {
            details.Add(new ProviderDetailDescriptor("Installed version", installedVersion));
        }

        details.Add(new ProviderDetailDescriptor("Default model", suggestedModelName));

        var availableModels = FormatAvailableModels(metadata?.AvailableModels);
        if (!string.IsNullOrWhiteSpace(availableModels))
        {
            details.Add(new ProviderDetailDescriptor("Available models", availableModels));
        }

        return details;
    }

    private static string FormatAvailableModels(IReadOnlyList<string>? models)
    {
        if (models is null || models.Count == 0)
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
