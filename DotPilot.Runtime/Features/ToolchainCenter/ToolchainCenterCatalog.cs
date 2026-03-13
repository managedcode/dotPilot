using DotPilot.Core.Features.ToolchainCenter;

namespace DotPilot.Runtime.Features.ToolchainCenter;

public sealed class ToolchainCenterCatalog : IToolchainCenterCatalog, IDisposable
{
    private const string EpicSummary =
        "Issue #14 keeps provider installation, auth, diagnostics, configuration, and polling visible before the first live session.";
    private const string UiWorkstreamName = "Toolchain Center UI";
    private const string UiWorkstreamSummary =
        "The settings shell exposes a first-class desktop Toolchain Center with provider cards, detail panes, and operator actions.";
    private const string DiagnosticsWorkstreamName = "Connection diagnostics";
    private const string DiagnosticsWorkstreamSummary =
        "Launch, connection, resume, tool access, and auth diagnostics stay attributable before live work starts.";
    private const string ConfigurationWorkstreamName = "Secrets and environment";
    private const string ConfigurationWorkstreamSummary =
        "Provider secrets, local overrides, and non-secret environment configuration stay visible without leaking values.";
    private const string PollingWorkstreamName = "Background polling";
    private const string PollingWorkstreamSummary =
        "Version and auth readiness refresh in the background so the workbench can surface stale state early.";
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly PeriodicTimer? _pollingTimer;
    private readonly Task _pollingTask;
    private ToolchainCenterSnapshot _snapshot;

    public ToolchainCenterCatalog()
        : this(TimeProvider.System, startBackgroundPolling: true)
    {
    }

    public ToolchainCenterCatalog(TimeProvider timeProvider, bool startBackgroundPolling)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        _timeProvider = timeProvider;
        _snapshot = EvaluateSnapshot();
        if (startBackgroundPolling)
        {
            _pollingTimer = new PeriodicTimer(TimeSpan.FromMinutes(5), timeProvider);
            _pollingTask = Task.Run(PollAsync);
        }
        else
        {
            _pollingTask = Task.CompletedTask;
        }
    }

    public ToolchainCenterSnapshot GetSnapshot() => _snapshot;

    public void Dispose()
    {
        _disposeTokenSource.Cancel();
        _pollingTimer?.Dispose();
        _disposeTokenSource.Dispose();
    }

    private async Task PollAsync()
    {
        if (_pollingTimer is null)
        {
            return;
        }

        try
        {
            while (await _pollingTimer.WaitForNextTickAsync(_disposeTokenSource.Token))
            {
                _snapshot = EvaluateSnapshot();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during app shutdown.
        }
    }

    private ToolchainCenterSnapshot EvaluateSnapshot()
    {
        var evaluatedAt = _timeProvider.GetUtcNow();
        var providers = ToolchainProviderSnapshotFactory.Create(evaluatedAt);
        return new(
            ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.ToolchainCenterEpic),
            EpicSummary,
            CreateWorkstreams(),
            providers,
            ToolchainProviderSnapshotFactory.CreateBackgroundPolling(providers, evaluatedAt),
            providers.Count(provider => provider.ReadinessState is ToolchainReadinessState.Ready),
            providers.Count(provider => provider.ReadinessState is not ToolchainReadinessState.Ready));
    }

    private static IReadOnlyList<ToolchainCenterWorkstreamDescriptor> CreateWorkstreams()
    {
        return
        [
            new(
                ToolchainCenterIssues.ToolchainCenterUi,
                ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.ToolchainCenterUi),
                UiWorkstreamName,
                UiWorkstreamSummary),
            new(
                ToolchainCenterIssues.ConnectionDiagnostics,
                ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.ConnectionDiagnostics),
                DiagnosticsWorkstreamName,
                DiagnosticsWorkstreamSummary),
            new(
                ToolchainCenterIssues.ProviderConfiguration,
                ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.ProviderConfiguration),
                ConfigurationWorkstreamName,
                ConfigurationWorkstreamSummary),
            new(
                ToolchainCenterIssues.BackgroundPolling,
                ToolchainCenterIssues.FormatIssueLabel(ToolchainCenterIssues.BackgroundPolling),
                PollingWorkstreamName,
                PollingWorkstreamSummary),
        ];
    }
}
