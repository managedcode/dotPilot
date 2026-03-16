using System.Collections.Immutable;
using System.Globalization;
using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Presentation;

public partial record ChatModel
{
    private const string LiveSessionsMetricLabel = "Live sessions";
    private const string ReadyProvidersMetricLabel = "Providers ready";
    private const string AttentionProvidersMetricLabel = "Needs attention";
    private const string EmptyLiveSessionsMessage = "No live sessions right now.";
    private const string LiveSessionsMetricSummary = "Sessions that are actively generating output.";
    private const string ReadyProvidersMetricSummary = "Enabled providers that are ready for local work.";
    private const string AttentionProvidersMetricSummary = "Enabled providers that need setup or recovery.";
    private AsyncCommand? _openFleetSessionCommand;

    public IState<FleetBoardView> FleetBoard => State.Async(this, LoadFleetBoardAsync, _sessionRefresh);

    public ICommand OpenFleetSessionCommand =>
        _openFleetSessionCommand ??= new AsyncCommand(
            parameter => OpenFleetSessionCore(parameter, CancellationToken.None));

    public ValueTask OpenFleetSession(FleetBoardSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return OpenFleetSessionCore(request, cancellationToken);
    }

    private void OnSessionActivityChanged(object? sender, EventArgs e)
    {
        uiDispatcher.Execute(_sessionRefresh.Raise);
    }

    private async ValueTask<FleetBoardView> LoadFleetBoardAsync(CancellationToken cancellationToken)
    {
        var activitySnapshot = sessionActivityMonitor.Current;
        var providers = await GetFleetProvidersAsync(cancellationToken);

        var liveSessions = activitySnapshot.ActiveSessions
            .Select(MapFleetSession)
            .ToImmutableArray();
        var providerItems = providers
            .Select(MapFleetProvider)
            .ToImmutableArray();

        return new FleetBoardView(
            CreateFleetMetrics(activitySnapshot.ActiveSessionCount, providers),
            liveSessions,
            providerItems,
            liveSessions.Length > 0,
            liveSessions.Length == 0,
            EmptyLiveSessionsMessage);
    }

    private async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetFleetProvidersAsync(CancellationToken cancellationToken)
    {
        if (hasFleetProviderSnapshot && !fleetProviderSnapshotStale)
        {
            return fleetProviderSnapshot;
        }

        await fleetProviderRefreshGate.WaitAsync(cancellationToken);
        try
        {
            if (hasFleetProviderSnapshot && !fleetProviderSnapshotStale)
            {
                return fleetProviderSnapshot;
            }

            var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                return hasFleetProviderSnapshot
                    ? fleetProviderSnapshot
                    : [];
            }

            fleetProviderSnapshot = [.. workspace.Providers];
            hasFleetProviderSnapshot = true;
            fleetProviderSnapshotStale = false;
            return fleetProviderSnapshot;
        }
        finally
        {
            fleetProviderRefreshGate.Release();
        }
    }

    private async ValueTask OpenFleetSessionCore(object? parameter, CancellationToken cancellationToken)
    {
        var request = parameter switch
        {
            FleetBoardSessionRequest value => value,
            SessionId sessionId => new FleetBoardSessionRequest(sessionId, string.Empty),
            _ => null,
        };
        if (request is null)
        {
            return;
        }

        var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
        if (!workspaceResult.TryGetValue(out var workspace))
        {
            return;
        }

        var sessions = workspace.Sessions
            .Select(MapSidebarItem)
            .ToImmutableArray();
        var selectedSession = FindSessionById(sessions, request.SessionId);
        if (IsEmptySelectedChat(selectedSession))
        {
            return;
        }

        await SelectedChat.UpdateAsync(_ => selectedSession, cancellationToken);
        await FeedbackMessage.SetAsync(string.Empty, cancellationToken);
        _sessionRefresh.Raise();
    }

    private static ImmutableArray<FleetBoardMetricItem> CreateFleetMetrics(
        int activeSessionCount,
        IReadOnlyList<ProviderStatusDescriptor> providers)
    {
        var enabledProviders = providers.Where(provider => provider.IsEnabled).ToImmutableArray();
        var readyProviders = enabledProviders.Count(provider => provider.Status == AgentProviderStatus.Ready);
        var attentionProviders = enabledProviders.Count(provider => provider.Status != AgentProviderStatus.Ready);

        return
        [
            new FleetBoardMetricItem(
                LiveSessionsMetricLabel,
                activeSessionCount.ToString(CultureInfo.InvariantCulture),
                LiveSessionsMetricSummary,
                FleetBoardAutomationIds.ForMetric(LiveSessionsMetricLabel)),
            new FleetBoardMetricItem(
                ReadyProvidersMetricLabel,
                readyProviders.ToString(CultureInfo.InvariantCulture),
                ReadyProvidersMetricSummary,
                FleetBoardAutomationIds.ForMetric(ReadyProvidersMetricLabel)),
            new FleetBoardMetricItem(
                AttentionProvidersMetricLabel,
                attentionProviders.ToString(CultureInfo.InvariantCulture),
                AttentionProvidersMetricSummary,
                FleetBoardAutomationIds.ForMetric(AttentionProvidersMetricLabel)),
        ];
    }

    private FleetBoardSessionItem MapFleetSession(SessionActivityDescriptor descriptor)
    {
        return new FleetBoardSessionItem(
            descriptor.SessionTitle,
            $"{descriptor.AgentName} · {descriptor.ProviderDisplayName}",
            FleetBoardAutomationIds.ForSession(descriptor.SessionTitle),
            new FleetBoardSessionRequest(descriptor.SessionId, descriptor.SessionTitle),
            OpenFleetSessionCommand);
    }

    private static FleetBoardProviderItem MapFleetProvider(ProviderStatusDescriptor provider)
    {
        return new FleetBoardProviderItem(
            provider.DisplayName,
            GetProviderStatusLabel(provider.Status),
            provider.StatusSummary,
            ResolveProviderStatusBrush(provider.Status),
            FleetBoardAutomationIds.ForProvider(provider.DisplayName));
    }

    private static string GetProviderStatusLabel(AgentProviderStatus status)
    {
        return status switch
        {
            AgentProviderStatus.Ready => "Ready",
            AgentProviderStatus.Disabled => "Disabled",
            AgentProviderStatus.RequiresSetup => "Setup needed",
            AgentProviderStatus.Unsupported => "Unsupported",
            AgentProviderStatus.Error => "Error",
            _ => "Unknown",
        };
    }

    private static Brush? ResolveProviderStatusBrush(AgentProviderStatus status)
    {
        return status switch
        {
            AgentProviderStatus.Ready => DesignBrushPalette.AccentBrush,
            AgentProviderStatus.Disabled => DesignBrushPalette.BadgeSurfaceBrush,
            AgentProviderStatus.RequiresSetup or AgentProviderStatus.Unsupported => DesignBrushPalette.AvatarVariantEmilyBrush,
            AgentProviderStatus.Error => DesignBrushPalette.AvatarVariantFrankBrush,
            _ => DesignBrushPalette.BadgeSurfaceBrush,
        };
    }
}
