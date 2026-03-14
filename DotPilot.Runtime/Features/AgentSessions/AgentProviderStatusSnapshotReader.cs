using DotPilot.Core.Features.AgentSessions;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Runtime.Features.AgentSessions;

internal static class AgentProviderStatusSnapshotReader
{
    private const string BrowserStatusSummary = "Desktop CLI probing is unavailable in the browser automation head.";
    private const string DisabledStatusSummary = "Provider is disabled for local agent creation.";
    private const string BuiltInStatusSummary = "Built in and ready for deterministic local testing.";
    private const string MissingCliSummaryFormat = "{0} CLI is not installed.";
    private const string ReadySummaryFormat = "{0} CLI is available on PATH.";
    private const string LiveExecutionUnavailableSummaryFormat =
        "{0} CLI is detected, but live session execution is not wired yet in this app slice.";
    private static readonly System.Text.CompositeFormat MissingCliSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(MissingCliSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadySummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(ReadySummaryFormat);
    private static readonly System.Text.CompositeFormat LiveExecutionUnavailableCompositeFormat =
        System.Text.CompositeFormat.Parse(LiveExecutionUnavailableSummaryFormat);

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
        string? installedVersion = null;
        string? executablePath = null;
        var status = AgentProviderStatus.Ready;
        var statusSummary = BuiltInStatusSummary;
        var canCreateAgents = true;

        if (OperatingSystem.IsBrowser() && !profile.IsBuiltIn)
        {
            actions.Add(new ProviderActionDescriptor("Install", "Run this on desktop.", profile.InstallCommand));
            status = AgentProviderStatus.Unsupported;
            statusSummary = BrowserStatusSummary;
            canCreateAgents = false;
        }
        else if (profile.IsBuiltIn)
        {
            installedVersion = profile.DefaultModelName;
        }
        else
        {
            executablePath = AgentSessionCommandProbe.ResolveExecutablePath(profile.CommandName);
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                actions.Add(new ProviderActionDescriptor("Install", "Install the CLI, then refresh settings.", profile.InstallCommand));
                status = AgentProviderStatus.RequiresSetup;
                statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, MissingCliSummaryCompositeFormat, profile.DisplayName);
                canCreateAgents = false;
            }
            else
            {
                installedVersion = AgentSessionCommandProbe.ReadVersion(executablePath, ["--version"]);
                actions.Add(new ProviderActionDescriptor("Open CLI", "CLI detected on PATH.", $"{profile.CommandName} --version"));
                if (profile.SupportsLiveExecution)
                {
                    statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadySummaryCompositeFormat, profile.DisplayName);
                }
                else
                {
                    status = AgentProviderStatus.Error;
                    statusSummary = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        LiveExecutionUnavailableCompositeFormat,
                        profile.DisplayName);
                    canCreateAgents = false;
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
                installedVersion,
                preference.IsEnabled,
                canCreateAgents,
                actions),
            executablePath);
    }
}

internal sealed record ProviderStatusProbeResult(
    ProviderStatusDescriptor Descriptor,
    string? ExecutablePath);
