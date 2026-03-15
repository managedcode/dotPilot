using System.Collections.Immutable;
using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class AgentWorkspaceState(
    IAgentSessionService agentSessionService,
    IAgentProviderStatusCache providerStatusCache,
    ILogger<AgentWorkspaceState> logger)
    : IAgentWorkspaceState, IDisposable
{
    private readonly SemaphoreSlim _cacheGate = new(1, 1);
    private readonly Dictionary<SessionId, SessionTranscriptSnapshot> _sessions = [];
    private AgentWorkspaceSnapshot? _workspace;

    public async ValueTask<AgentWorkspaceSnapshot> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_workspace is { } cachedWorkspace)
        {
            LogWorkspaceCacheHit(cachedWorkspace);
            return cachedWorkspace;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is { } gatedWorkspace)
            {
                LogWorkspaceCacheHit(gatedWorkspace);
                return gatedWorkspace;
            }

            return await LoadWorkspaceAsync(forceRefresh: false, cancellationToken);
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    public async ValueTask<AgentWorkspaceSnapshot> RefreshWorkspaceAsync(CancellationToken cancellationToken)
    {
        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            return await LoadWorkspaceAsync(forceRefresh: true, cancellationToken);
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    public async ValueTask<SessionTranscriptSnapshot?> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(sessionId, out var cachedSession))
        {
            AgentWorkspaceStateLog.SessionCacheHit(logger, sessionId, cachedSession.Entries.Count);
            return cachedSession;
        }

        var session = await agentSessionService.GetSessionAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            _sessions[sessionId] = session;
            _workspace = MergeWorkspaceSession(_workspace, session.Session, selectSession: false);
        }
        finally
        {
            _cacheGate.Release();
        }

        return session;
    }

    public async ValueTask<AgentProfileSummary> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        var created = await agentSessionService.CreateAgentAsync(command, cancellationToken);

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is not null)
            {
                var agents = OrderAgents(_workspace.Agents.Append(created))
                    .ToImmutableArray();
                _workspace = _workspace with
                {
                    Agents = agents,
                };
            }
        }
        finally
        {
            _cacheGate.Release();
        }

        AgentWorkspaceStateLog.AgentCached(logger, created.Id, created.ProviderKind);

        return created;
    }

    public async ValueTask<SessionTranscriptSnapshot> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var created = await agentSessionService.CreateSessionAsync(command, cancellationToken);

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            _sessions[created.Session.Id] = created;
            _workspace = MergeWorkspaceSession(_workspace, created.Session, selectSession: true);
        }
        finally
        {
            _cacheGate.Release();
        }

        AgentWorkspaceStateLog.SessionCached(logger, created.Session.Id, created.Session.PrimaryAgentId);

        return created;
    }

    public async ValueTask<ProviderStatusDescriptor> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        var provider = await agentSessionService.UpdateProviderAsync(command, cancellationToken);

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is not null)
            {
                var providers = _workspace.Providers
                    .Select(existing => existing.Kind == provider.Kind ? provider : existing)
                    .ToImmutableArray();
                _workspace = _workspace with
                {
                    Providers = providers,
                };
            }
        }
        finally
        {
            _cacheGate.Release();
        }

        AgentWorkspaceStateLog.ProviderCached(logger, provider.Kind, provider.IsEnabled);

        return provider;
    }

    public async IAsyncEnumerable<SessionStreamEntry> SendMessageAsync(
        SendSessionMessageCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _ = await GetSessionAsync(command.SessionId, cancellationToken);

        await foreach (var entry in agentSessionService.SendMessageAsync(command, cancellationToken))
        {
            await _cacheGate.WaitAsync(cancellationToken);
            try
            {
                if (_sessions.TryGetValue(command.SessionId, out var cachedSession))
                {
                    var updatedSession = cachedSession with
                    {
                        Entries = UpsertEntries(cachedSession.Entries, entry),
                    };
                    _sessions[command.SessionId] = updatedSession;
                    _workspace = MergeWorkspaceSession(
                        _workspace,
                        UpdateSessionPreview(updatedSession.Session, entry),
                        selectSession: false);
                }
            }
            finally
            {
                _cacheGate.Release();
            }

            yield return entry;
        }
    }

    private static AgentWorkspaceSnapshot? MergeWorkspaceSession(
        AgentWorkspaceSnapshot? workspace,
        SessionListItem session,
        bool selectSession)
    {
        if (workspace is null)
        {
            return null;
        }

        var sessions = workspace.Sessions
            .Where(existing => existing.Id != session.Id)
            .Append(session)
            .OrderByDescending(existing => existing.UpdatedAt)
            .ToImmutableArray();

        return workspace with
        {
            Sessions = sessions,
            SelectedSessionId = selectSession ? session.Id : workspace.SelectedSessionId ?? session.Id,
        };
    }

    private static IEnumerable<AgentProfileSummary> OrderAgents(IEnumerable<AgentProfileSummary> agents)
    {
        var orderedAgents = agents.ToList();
        var hasNonSystemAgents = orderedAgents.Any(agent => !AgentSessionDefaults.IsSystemAgent(agent.Name));

        return orderedAgents
            .OrderBy(agent => hasNonSystemAgents && AgentSessionDefaults.IsSystemAgent(agent.Name) ? 1 : 0)
            .ThenByDescending(agent => agent.CreatedAt)
            .ThenBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static ImmutableArray<SessionStreamEntry> UpsertEntries(
        IReadOnlyList<SessionStreamEntry> entries,
        SessionStreamEntry entry)
    {
        var updatedEntries = entries.ToList();
        var existingIndex = updatedEntries.FindIndex(existing => existing.Id == entry.Id);
        if (existingIndex >= 0)
        {
            updatedEntries[existingIndex] = entry;
        }
        else
        {
            updatedEntries.Add(entry);
        }

        return updatedEntries
            .OrderBy(existing => existing.Timestamp)
            .ToImmutableArray();
    }

    private static SessionListItem UpdateSessionPreview(SessionListItem session, SessionStreamEntry entry)
    {
        return session with
        {
            Preview = string.IsNullOrWhiteSpace(entry.Text) ? session.Preview : entry.Text,
            UpdatedAt = entry.Timestamp,
        };
    }

    public void Dispose()
    {
        _cacheGate.Dispose();
    }

    private async ValueTask<AgentWorkspaceSnapshot> LoadWorkspaceAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (forceRefresh)
        {
            await providerStatusCache.RefreshAsync(cancellationToken);
        }

        var workspace = await agentSessionService.GetWorkspaceAsync(cancellationToken);
        _workspace = workspace;
        AgentWorkspaceStateLog.WorkspaceRefreshed(
            logger,
            workspace.Sessions.Count,
            workspace.Agents.Count,
            workspace.Providers.Count);
        return workspace;
    }

    private void LogWorkspaceCacheHit(AgentWorkspaceSnapshot workspace)
    {
        AgentWorkspaceStateLog.WorkspaceCacheHit(
            logger,
            workspace.Sessions.Count,
            workspace.Agents.Count,
            workspace.Providers.Count);
    }
}
