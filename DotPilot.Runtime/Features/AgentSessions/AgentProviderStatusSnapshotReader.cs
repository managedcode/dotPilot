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
    private static readonly System.Text.CompositeFormat MissingCliSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(MissingCliSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadySummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(ReadySummaryFormat);

    public static async Task<IReadOnlyList<ProviderStatusDescriptor>> BuildAsync(
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

    public static async Task<bool> IsEnabledAsync(
        LocalAgentSessionDbContext dbContext,
        AgentProviderKind providerKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var record = await dbContext.ProviderPreferences
            .FirstOrDefaultAsync(
                preference => preference.ProviderKind == (int)providerKind,
                cancellationToken);

        return record?.IsEnabled == true;
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

    private static ProviderStatusDescriptor BuildProviderStatus(
        AgentSessionProviderProfile profile,
        ProviderPreferenceRecord preference)
    {
        var providerId = AgentSessionDeterministicIdentity.CreateProviderId(profile.CommandName);
        var actions = new List<ProviderActionDescriptor>();
        string? installedVersion = null;
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
            var executablePath = AgentSessionCommandProbe.ResolveExecutablePath(profile.CommandName);
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
                statusSummary = string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadySummaryCompositeFormat, profile.DisplayName);
            }
        }

        if (!preference.IsEnabled)
        {
            status = AgentProviderStatus.Disabled;
            statusSummary = $"{DisabledStatusSummary} {statusSummary}";
            canCreateAgents = false;
        }

        return new ProviderStatusDescriptor(
            providerId,
            profile.Kind,
            profile.DisplayName,
            profile.CommandName,
            status,
            statusSummary,
            installedVersion,
            preference.IsEnabled,
            canCreateAgents,
            actions);
    }
}
