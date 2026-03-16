using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed class SessionActivityMonitor(ILogger<SessionActivityMonitor> logger) : ISessionActivityMonitor
{
    private static readonly SessionActivitySnapshot EmptySnapshot = new(
        false,
        0,
        [],
        null,
        string.Empty,
        null,
        string.Empty,
        string.Empty);

    private readonly Lock _gate = new();
    private readonly List<ActivityLease> _leases = [];
    private SessionActivitySnapshot _current = EmptySnapshot;

    public SessionActivitySnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public event EventHandler? StateChanged;

    public IDisposable BeginActivity(SessionActivityDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ActivityLease lease;
        int activeSessionCount;
        lock (_gate)
        {
            lease = new ActivityLease(this, descriptor);
            _leases.Add(lease);
            activeSessionCount = UpdateSnapshotUnsafe().ActiveSessionCount;
        }

        SessionActivityMonitorLog.ActivityStarted(
            logger,
            descriptor.SessionId.Value,
            descriptor.AgentProfileId.Value,
            activeSessionCount);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return lease;
    }

    private void EndActivity(ActivityLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        int activeSessionCount;
        lock (_gate)
        {
            var index = _leases.IndexOf(lease);
            if (index < 0)
            {
                return;
            }

            _leases.RemoveAt(index);
            activeSessionCount = UpdateSnapshotUnsafe().ActiveSessionCount;
        }

        SessionActivityMonitorLog.ActivityCompleted(
            logger,
            lease.Descriptor.SessionId.Value,
            lease.Descriptor.AgentProfileId.Value,
            activeSessionCount);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private SessionActivitySnapshot UpdateSnapshotUnsafe()
    {
        var activeSessions = GetActiveSessionsUnsafe();
        _current = _leases.Count == 0
            ? EmptySnapshot
            : CreateSnapshot(activeSessions);
        return _current;
    }

    private IReadOnlyList<SessionActivityDescriptor> GetActiveSessionsUnsafe()
    {
        if (_leases.Count == 0)
        {
            return [];
        }

        var descriptors = new Dictionary<SessionId, SessionActivityDescriptor>();
        for (var index = 0; index < _leases.Count; index++)
        {
            var descriptor = _leases[index].Descriptor;
            descriptors.Remove(descriptor.SessionId);
            descriptors[descriptor.SessionId] = descriptor;
        }

        return [.. descriptors.Values];
    }

    private static SessionActivitySnapshot CreateSnapshot(IReadOnlyList<SessionActivityDescriptor> activeSessions)
    {
        var latestSession = activeSessions[^1];
        return new SessionActivitySnapshot(
            true,
            activeSessions.Count,
            activeSessions,
            latestSession.SessionId,
            latestSession.SessionTitle,
            latestSession.AgentProfileId,
            latestSession.AgentName,
            latestSession.ProviderDisplayName);
    }

    private sealed class ActivityLease(SessionActivityMonitor owner, SessionActivityDescriptor descriptor) : IDisposable
    {
        private int _disposed;

        public SessionActivityDescriptor Descriptor { get; } = descriptor;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            owner.EndActivity(this);
        }
    }
}
