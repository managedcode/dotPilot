using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Core.Features.ToolchainCenter;

public sealed record ToolchainCenterWorkstreamDescriptor(
    int IssueNumber,
    string SectionLabel,
    string Name,
    string Summary);

public sealed record ToolchainActionDescriptor(
    string Title,
    string Summary,
    ToolchainActionKind Kind,
    bool IsPrimary,
    bool IsEnabled);

public sealed record ToolchainDiagnosticDescriptor(
    string Name,
    ToolchainDiagnosticStatus Status,
    string Summary);

public sealed record ToolchainConfigurationEntry(
    string Name,
    string ValueDisplay,
    string Summary,
    ToolchainConfigurationKind Kind,
    ToolchainConfigurationStatus Status,
    bool IsSensitive);

public sealed record ToolchainPollingDescriptor(
    TimeSpan RefreshInterval,
    DateTimeOffset LastRefreshAt,
    DateTimeOffset NextRefreshAt,
    ToolchainPollingStatus Status,
    string Summary);

public sealed record ToolchainProviderSnapshot(
    int IssueNumber,
    string SectionLabel,
    ProviderDescriptor Provider,
    string ExecutablePath,
    string InstalledVersion,
    ToolchainReadinessState ReadinessState,
    string ReadinessSummary,
    ToolchainVersionStatus VersionStatus,
    string VersionSummary,
    ToolchainAuthStatus AuthStatus,
    string AuthSummary,
    ToolchainHealthStatus HealthStatus,
    string HealthSummary,
    IReadOnlyList<ToolchainActionDescriptor> Actions,
    IReadOnlyList<ToolchainDiagnosticDescriptor> Diagnostics,
    IReadOnlyList<ToolchainConfigurationEntry> Configuration,
    ToolchainPollingDescriptor Polling);

public sealed record ToolchainCenterSnapshot(
    string EpicLabel,
    string Summary,
    IReadOnlyList<ToolchainCenterWorkstreamDescriptor> Workstreams,
    IReadOnlyList<ToolchainProviderSnapshot> Providers,
    ToolchainPollingDescriptor BackgroundPolling,
    int ReadyProviderCount,
    int AttentionRequiredProviderCount);
