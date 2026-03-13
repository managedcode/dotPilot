using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.ToolchainCenter;

namespace DotPilot.Runtime.Features.ToolchainCenter;

internal static class ToolchainProviderSnapshotFactory
{
    private static readonly TimeSpan BackgroundRefreshInterval = TimeSpan.FromMinutes(5);
    private const string InstallActionTitleFormat = "Install {0}";
    private const string ConnectActionTitleFormat = "Connect {0}";
    private const string UpdateActionTitleFormat = "Update {0}";
    private const string TestActionTitleFormat = "Test {0}";
    private const string TroubleshootActionTitleFormat = "Troubleshoot {0}";
    private const string DocsActionTitleFormat = "Review {0} setup";
    private const string MissingExecutablePath = "Not detected";
    private const string MissingVersion = "Unavailable";
    private const string MissingVersionSummary = "Install the CLI before version checks can run.";
    private const string UnknownVersionSummary = "The executable is present, but the version could not be confirmed automatically.";
    private const string VersionSummaryFormat = "Detected version {0}.";
    private const string AuthMissingSummary = "No non-interactive authentication signal was detected.";
    private const string AuthConnectedSummary = "A non-interactive authentication signal is configured.";
    private const string ReadinessMissingSummaryFormat = "{0} is not installed on PATH.";
    private const string ReadinessAuthRequiredSummaryFormat = "{0} is installed, but authentication still needs operator attention.";
    private const string ReadinessLimitedSummaryFormat = "{0} is installed, but one or more readiness prerequisites still need attention.";
    private const string ReadinessReadySummaryFormat = "{0} is ready for pre-session operator checks.";
    private const string HealthBlockedMissingSummaryFormat = "{0} launch is blocked until the CLI is installed.";
    private const string HealthBlockedAuthSummaryFormat = "{0} launch is blocked until authentication is configured.";
    private const string HealthWarningSummaryFormat = "{0} is installed, but diagnostics still show warnings.";
    private const string HealthReadySummaryFormat = "{0} passed the available pre-session readiness checks.";
    private const string LaunchDiagnosticName = "Launch";
    private const string VersionDiagnosticName = "Version";
    private const string AuthDiagnosticName = "Authentication";
    private const string ConnectionDiagnosticName = "Connection test";
    private const string ResumeDiagnosticName = "Resume test";
    private const string LaunchPassedSummary = "The executable is installed and launchable from PATH.";
    private const string LaunchFailedSummary = "The executable is not available on PATH.";
    private const string VersionFailedSummary = "The version could not be resolved automatically.";
    private const string ConnectionReadySummary = "The provider is ready for a live connection test from the Toolchain Center.";
    private const string ConnectionBlockedSummary = "Fix installation and authentication before running a live connection test.";
    private const string ResumeReadySummary = "Resume diagnostics can run after the connection test succeeds.";
    private const string ResumeBlockedSummary = "Resume diagnostics stay blocked until the connection test is ready.";
    private const string BackgroundPollingSummaryFormat = "Background polling refreshes every {0} minutes to surface stale versions and broken auth state.";
    private const string ProviderPollingHealthySummaryFormat = "Readiness was checked just now. The next background refresh runs in {0} minutes.";
    private const string ProviderPollingWarningSummaryFormat = "Readiness needs attention. The next background refresh runs in {0} minutes.";
    private static readonly System.Text.CompositeFormat InstallActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(InstallActionTitleFormat);
    private static readonly System.Text.CompositeFormat ConnectActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(ConnectActionTitleFormat);
    private static readonly System.Text.CompositeFormat UpdateActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(UpdateActionTitleFormat);
    private static readonly System.Text.CompositeFormat TestActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(TestActionTitleFormat);
    private static readonly System.Text.CompositeFormat TroubleshootActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(TroubleshootActionTitleFormat);
    private static readonly System.Text.CompositeFormat DocsActionTitleCompositeFormat = System.Text.CompositeFormat.Parse(DocsActionTitleFormat);
    private static readonly System.Text.CompositeFormat VersionSummaryCompositeFormat = System.Text.CompositeFormat.Parse(VersionSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadinessMissingSummaryCompositeFormat = System.Text.CompositeFormat.Parse(ReadinessMissingSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadinessAuthRequiredSummaryCompositeFormat = System.Text.CompositeFormat.Parse(ReadinessAuthRequiredSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadinessLimitedSummaryCompositeFormat = System.Text.CompositeFormat.Parse(ReadinessLimitedSummaryFormat);
    private static readonly System.Text.CompositeFormat ReadinessReadySummaryCompositeFormat = System.Text.CompositeFormat.Parse(ReadinessReadySummaryFormat);
    private static readonly System.Text.CompositeFormat HealthBlockedMissingSummaryCompositeFormat = System.Text.CompositeFormat.Parse(HealthBlockedMissingSummaryFormat);
    private static readonly System.Text.CompositeFormat HealthBlockedAuthSummaryCompositeFormat = System.Text.CompositeFormat.Parse(HealthBlockedAuthSummaryFormat);
    private static readonly System.Text.CompositeFormat HealthWarningSummaryCompositeFormat = System.Text.CompositeFormat.Parse(HealthWarningSummaryFormat);
    private static readonly System.Text.CompositeFormat HealthReadySummaryCompositeFormat = System.Text.CompositeFormat.Parse(HealthReadySummaryFormat);
    private static readonly System.Text.CompositeFormat BackgroundPollingSummaryCompositeFormat = System.Text.CompositeFormat.Parse(BackgroundPollingSummaryFormat);
    private static readonly System.Text.CompositeFormat ProviderPollingHealthySummaryCompositeFormat = System.Text.CompositeFormat.Parse(ProviderPollingHealthySummaryFormat);
    private static readonly System.Text.CompositeFormat ProviderPollingWarningSummaryCompositeFormat = System.Text.CompositeFormat.Parse(ProviderPollingWarningSummaryFormat);

    public static IReadOnlyList<ToolchainProviderSnapshot> Create(DateTimeOffset evaluatedAt)
    {
        return ToolchainProviderProfiles.All
            .Select(profile => Create(profile, evaluatedAt))
            .ToArray();
    }

    public static ToolchainPollingDescriptor CreateBackgroundPolling(IReadOnlyList<ToolchainProviderSnapshot> providers, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var status = providers.Any(provider => provider.ReadinessState is not ToolchainReadinessState.Ready)
            ? ToolchainPollingStatus.Warning
            : ToolchainPollingStatus.Healthy;

        return new(
            BackgroundRefreshInterval,
            evaluatedAt,
            evaluatedAt.Add(BackgroundRefreshInterval),
            status,
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                BackgroundPollingSummaryCompositeFormat,
                BackgroundRefreshInterval.TotalMinutes));
    }

    private static ToolchainProviderSnapshot Create(ToolchainProviderProfile profile, DateTimeOffset evaluatedAt)
    {
        var executablePath = ToolchainCommandProbe.ResolveExecutablePath(profile.CommandName);
        var isInstalled = !string.IsNullOrWhiteSpace(executablePath);
        var installedVersion = isInstalled
            ? ToolchainCommandProbe.ReadVersion(executablePath!, profile.VersionArguments)
            : string.Empty;
        var authConfigured = profile.AuthenticationEnvironmentVariables
            .Select(Environment.GetEnvironmentVariable)
            .Any(static value => !string.IsNullOrWhiteSpace(value));
        var toolAccessAvailable = isInstalled && (
            profile.ToolAccessArguments.Count == 0 ||
            ToolchainCommandProbe.CanExecute(executablePath!, profile.ToolAccessArguments));

        var providerStatus = ResolveProviderStatus(isInstalled, authConfigured, toolAccessAvailable);
        var readinessState = ResolveReadinessState(isInstalled, authConfigured, toolAccessAvailable, installedVersion);
        var versionStatus = ResolveVersionStatus(isInstalled, installedVersion);
        var authStatus = authConfigured ? ToolchainAuthStatus.Connected : ToolchainAuthStatus.Missing;
        var healthStatus = ResolveHealthStatus(isInstalled, authConfigured, toolAccessAvailable, installedVersion);
        var polling = CreateProviderPolling(evaluatedAt, readinessState);

        return new(
            profile.IssueNumber,
            ToolchainCenterIssues.FormatIssueLabel(profile.IssueNumber),
            new ProviderDescriptor
            {
                Id = ToolchainDeterministicIdentity.CreateProviderId(profile.CommandName),
                DisplayName = profile.DisplayName,
                CommandName = profile.CommandName,
                Status = providerStatus,
                StatusSummary = ResolveReadinessSummary(profile.DisplayName, readinessState),
                RequiresExternalToolchain = true,
            },
            executablePath ?? MissingExecutablePath,
            string.IsNullOrWhiteSpace(installedVersion) ? MissingVersion : installedVersion,
            readinessState,
            ResolveReadinessSummary(profile.DisplayName, readinessState),
            versionStatus,
            ResolveVersionSummary(versionStatus, installedVersion),
            authStatus,
            authConfigured ? AuthConnectedSummary : AuthMissingSummary,
            healthStatus,
            ResolveHealthSummary(profile.DisplayName, healthStatus, authConfigured),
            CreateActions(profile, readinessState),
            CreateDiagnostics(profile, isInstalled, authConfigured, installedVersion, toolAccessAvailable),
            CreateConfiguration(profile),
            polling);
    }

    private static ProviderConnectionStatus ResolveProviderStatus(bool isInstalled, bool authConfigured, bool toolAccessAvailable)
    {
        if (!isInstalled)
        {
            return ProviderConnectionStatus.Unavailable;
        }

        if (!authConfigured)
        {
            return ProviderConnectionStatus.RequiresAuthentication;
        }

        return toolAccessAvailable
            ? ProviderConnectionStatus.Available
            : ProviderConnectionStatus.Misconfigured;
    }

    private static ToolchainReadinessState ResolveReadinessState(
        bool isInstalled,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        if (!isInstalled)
        {
            return ToolchainReadinessState.Missing;
        }

        if (!authConfigured)
        {
            return ToolchainReadinessState.ActionRequired;
        }

        if (!toolAccessAvailable || string.IsNullOrWhiteSpace(installedVersion))
        {
            return ToolchainReadinessState.Limited;
        }

        return ToolchainReadinessState.Ready;
    }

    private static ToolchainVersionStatus ResolveVersionStatus(bool isInstalled, string installedVersion)
    {
        if (!isInstalled)
        {
            return ToolchainVersionStatus.Missing;
        }

        return string.IsNullOrWhiteSpace(installedVersion)
            ? ToolchainVersionStatus.Unknown
            : ToolchainVersionStatus.Detected;
    }

    private static ToolchainHealthStatus ResolveHealthStatus(
        bool isInstalled,
        bool authConfigured,
        bool toolAccessAvailable,
        string installedVersion)
    {
        if (!isInstalled || !authConfigured)
        {
            return ToolchainHealthStatus.Blocked;
        }

        return toolAccessAvailable && !string.IsNullOrWhiteSpace(installedVersion)
            ? ToolchainHealthStatus.Healthy
            : ToolchainHealthStatus.Warning;
    }

    private static string ResolveReadinessSummary(string displayName, ToolchainReadinessState readinessState) =>
        readinessState switch
        {
            ToolchainReadinessState.Missing => string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadinessMissingSummaryCompositeFormat, displayName),
            ToolchainReadinessState.ActionRequired => string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadinessAuthRequiredSummaryCompositeFormat, displayName),
            ToolchainReadinessState.Limited => string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadinessLimitedSummaryCompositeFormat, displayName),
            _ => string.Format(System.Globalization.CultureInfo.InvariantCulture, ReadinessReadySummaryCompositeFormat, displayName),
        };

    private static string ResolveVersionSummary(ToolchainVersionStatus versionStatus, string installedVersion) =>
        versionStatus switch
        {
            ToolchainVersionStatus.Missing => MissingVersionSummary,
            ToolchainVersionStatus.Unknown => UnknownVersionSummary,
            _ => string.Format(System.Globalization.CultureInfo.InvariantCulture, VersionSummaryCompositeFormat, installedVersion),
        };

    private static string ResolveHealthSummary(string displayName, ToolchainHealthStatus healthStatus, bool authConfigured) =>
        healthStatus switch
        {
            ToolchainHealthStatus.Blocked when authConfigured => string.Format(System.Globalization.CultureInfo.InvariantCulture, HealthBlockedMissingSummaryCompositeFormat, displayName),
            ToolchainHealthStatus.Blocked => string.Format(System.Globalization.CultureInfo.InvariantCulture, HealthBlockedAuthSummaryCompositeFormat, displayName),
            ToolchainHealthStatus.Warning => string.Format(System.Globalization.CultureInfo.InvariantCulture, HealthWarningSummaryCompositeFormat, displayName),
            _ => string.Format(System.Globalization.CultureInfo.InvariantCulture, HealthReadySummaryCompositeFormat, displayName),
        };

    private static ToolchainActionDescriptor[] CreateActions(
        ToolchainProviderProfile profile,
        ToolchainReadinessState readinessState)
    {
        var installEnabled = readinessState is ToolchainReadinessState.Missing;
        var connectEnabled = readinessState is ToolchainReadinessState.ActionRequired or ToolchainReadinessState.Limited or ToolchainReadinessState.Ready;
        var testEnabled = readinessState is ToolchainReadinessState.Limited or ToolchainReadinessState.Ready;

        return
        [
            new(
                FormatDisplayName(InstallActionTitleCompositeFormat, profile.DisplayName),
                "Install the provider CLI before the first live session.",
                ToolchainActionKind.Install,
                IsPrimary: installEnabled,
                IsEnabled: installEnabled),
            new(
                FormatDisplayName(ConnectActionTitleCompositeFormat, profile.DisplayName),
                "Configure authentication so dotPilot can verify readiness before session start.",
                ToolchainActionKind.Connect,
                IsPrimary: readinessState is ToolchainReadinessState.ActionRequired,
                IsEnabled: connectEnabled),
            new(
                FormatDisplayName(UpdateActionTitleCompositeFormat, profile.DisplayName),
                "Recheck the installed version and apply provider updates outside the app when required.",
                ToolchainActionKind.Update,
                IsPrimary: false,
                IsEnabled: connectEnabled),
            new(
                FormatDisplayName(TestActionTitleCompositeFormat, profile.DisplayName),
                "Run the provider connection diagnostics before opening a live session.",
                ToolchainActionKind.TestConnection,
                IsPrimary: readinessState is ToolchainReadinessState.Ready,
                IsEnabled: testEnabled),
            new(
                FormatDisplayName(TroubleshootActionTitleCompositeFormat, profile.DisplayName),
                "Inspect prerequisites, broken auth, and blocked diagnostics without leaving the Toolchain Center.",
                ToolchainActionKind.Troubleshoot,
                IsPrimary: false,
                IsEnabled: true),
            new(
                FormatDisplayName(DocsActionTitleCompositeFormat, profile.DisplayName),
                "Review the provider setup guidance and operator runbook notes.",
                ToolchainActionKind.OpenDocs,
                IsPrimary: false,
                IsEnabled: true),
        ];
    }

    private static ToolchainDiagnosticDescriptor[] CreateDiagnostics(
        ToolchainProviderProfile profile,
        bool isInstalled,
        bool authConfigured,
        string installedVersion,
        bool toolAccessAvailable)
    {
        var launchPassed = isInstalled;
        var versionPassed = !string.IsNullOrWhiteSpace(installedVersion);
        var connectionReady = launchPassed && authConfigured;
        var resumeReady = connectionReady;

        return
        [
            new(LaunchDiagnosticName, launchPassed ? ToolchainDiagnosticStatus.Passed : ToolchainDiagnosticStatus.Failed, launchPassed ? LaunchPassedSummary : LaunchFailedSummary),
            new(VersionDiagnosticName, launchPassed ? (versionPassed ? ToolchainDiagnosticStatus.Passed : ToolchainDiagnosticStatus.Warning) : ToolchainDiagnosticStatus.Blocked, versionPassed ? ResolveVersionSummary(ToolchainVersionStatus.Detected, installedVersion) : VersionFailedSummary),
            new(AuthDiagnosticName, launchPassed ? (authConfigured ? ToolchainDiagnosticStatus.Passed : ToolchainDiagnosticStatus.Warning) : ToolchainDiagnosticStatus.Blocked, authConfigured ? AuthConnectedSummary : AuthMissingSummary),
            new(profile.ToolAccessDiagnosticName, launchPassed ? (toolAccessAvailable ? ToolchainDiagnosticStatus.Passed : ToolchainDiagnosticStatus.Warning) : ToolchainDiagnosticStatus.Blocked, toolAccessAvailable ? profile.ToolAccessReadySummary : profile.ToolAccessBlockedSummary),
            new(ConnectionDiagnosticName, connectionReady ? ToolchainDiagnosticStatus.Ready : ToolchainDiagnosticStatus.Blocked, connectionReady ? ConnectionReadySummary : ConnectionBlockedSummary),
            new(ResumeDiagnosticName, resumeReady ? ToolchainDiagnosticStatus.Ready : ToolchainDiagnosticStatus.Blocked, resumeReady ? ResumeReadySummary : ResumeBlockedSummary),
        ];
    }

    private static ToolchainConfigurationEntry[] CreateConfiguration(ToolchainProviderProfile profile)
    {
        var resolvedPath = ToolchainCommandProbe.ResolveExecutablePath(profile.CommandName);

        return profile.ConfigurationSignals
            .Select(signal =>
            {
                var isConfigured = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(signal.Name));
                var valueDisplay = signal.IsSensitive
                    ? (isConfigured ? "Configured" : "Missing")
                    : (Environment.GetEnvironmentVariable(signal.Name) ?? "Not set");

                return new ToolchainConfigurationEntry(
                    signal.Name,
                    valueDisplay,
                    signal.Summary,
                    signal.Kind,
                    ResolveConfigurationStatus(signal, isConfigured),
                    signal.IsSensitive);
            })
            .Append(
                new ToolchainConfigurationEntry(
                    $"{profile.CommandName} path",
                    resolvedPath ?? MissingExecutablePath,
                    "Resolved executable path for the provider CLI.",
                    ToolchainConfigurationKind.Setting,
                    resolvedPath is null
                        ? ToolchainConfigurationStatus.Missing
                        : ToolchainConfigurationStatus.Configured,
                    IsSensitive: false))
            .ToArray();
    }

    private static ToolchainConfigurationStatus ResolveConfigurationStatus(ToolchainConfigurationSignal signal, bool isConfigured)
    {
        if (isConfigured)
        {
            return ToolchainConfigurationStatus.Configured;
        }

        return signal.IsRequiredForReadiness
            ? ToolchainConfigurationStatus.Missing
            : ToolchainConfigurationStatus.Partial;
    }

    private static ToolchainPollingDescriptor CreateProviderPolling(
        DateTimeOffset evaluatedAt,
        ToolchainReadinessState readinessState)
    {
        var status = readinessState is ToolchainReadinessState.Ready
            ? ToolchainPollingStatus.Healthy
            : ToolchainPollingStatus.Warning;

        return new(
            BackgroundRefreshInterval,
            evaluatedAt,
            evaluatedAt.Add(BackgroundRefreshInterval),
            status,
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                readinessState is ToolchainReadinessState.Ready
                    ? ProviderPollingHealthySummaryCompositeFormat
                    : ProviderPollingWarningSummaryCompositeFormat,
                BackgroundRefreshInterval.TotalMinutes));
    }

    private static string FormatDisplayName(System.Text.CompositeFormat compositeFormat, string displayName) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, compositeFormat, displayName);
}
