using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed class SessionActivityMonitor(ILogger<SessionActivityMonitor> logger) : ISessionActivityMonitor
{
    private static readonly SessionActivitySnapshot EmptySnapshot = new(
        false,
        0,
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
        _current = _leases.Count == 0
            ? EmptySnapshot
            : CreateSnapshot(_leases[^1].Descriptor, _leases.Count);
        return _current;
    }

    private static SessionActivitySnapshot CreateSnapshot(SessionActivityDescriptor descriptor, int activeSessionCount)
    {
        return new SessionActivitySnapshot(
            true,
            activeSessionCount,
            descriptor.SessionId,
            descriptor.SessionTitle,
            descriptor.AgentProfileId,
            descriptor.AgentName,
            descriptor.ProviderDisplayName);
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
