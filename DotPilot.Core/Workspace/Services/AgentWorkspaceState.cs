using System.Collections.Immutable;
using DotPilot.Core.ControlPlaneDomain;
using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ChatSessions;
using ManagedCode.Communication;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.Workspace;

internal sealed class AgentWorkspaceState(
    IAgentSessionService agentSessionService,
    IAgentProviderStatusCache providerStatusCache,
    ILogger<AgentWorkspaceState> logger)
    : IAgentWorkspaceState, IDisposable
{
    private readonly SemaphoreSlim _cacheGate = new(1, 1);
    private readonly Dictionary<SessionId, SessionTranscriptSnapshot> _sessions = [];
    private AgentWorkspaceSnapshot? _workspace;

    public async ValueTask<Result<AgentWorkspaceSnapshot>> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (_workspace is { } cachedWorkspace)
        {
            LogWorkspaceCacheHit(cachedWorkspace);
            return Result<AgentWorkspaceSnapshot>.Succeed(cachedWorkspace);
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is { } gatedWorkspace)
            {
                LogWorkspaceCacheHit(gatedWorkspace);
                return Result<AgentWorkspaceSnapshot>.Succeed(gatedWorkspace);
            }

            return await LoadWorkspaceAsync(forceRefresh: false, cancellationToken);
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    public async ValueTask<Result<AgentWorkspaceSnapshot>> RefreshWorkspaceAsync(CancellationToken cancellationToken)
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

    public async ValueTask<Result<SessionTranscriptSnapshot>> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(sessionId, out var cachedSession))
        {
            AgentWorkspaceStateLog.SessionCacheHit(logger, sessionId, cachedSession.Entries.Count);
            return Result<SessionTranscriptSnapshot>.Succeed(cachedSession);
        }

        var session = await agentSessionService.GetSessionAsync(sessionId, cancellationToken);
        if (session.IsFailed)
        {
            return session;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            _sessions[sessionId] = session.Value;
            _workspace = MergeWorkspaceSession(_workspace, session.Value.Session, selectSession: false);
        }
        finally
        {
            _cacheGate.Release();
        }

        return session;
    }

    public async ValueTask<Result<AgentProfileSummary>> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        var created = await agentSessionService.CreateAgentAsync(command, cancellationToken);
        if (created.IsFailed)
        {
            return created;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is not null)
            {
                var agents = OrderAgents(_workspace.Agents.Append(created.Value))
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

        AgentWorkspaceStateLog.AgentCached(logger, created.Value.Id, created.Value.ProviderKind);

        return created;
    }

    public async ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var created = await agentSessionService.CreateSessionAsync(command, cancellationToken);
        if (created.IsFailed)
        {
            return created;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            _sessions[created.Value.Session.Id] = created.Value;
            _workspace = MergeWorkspaceSession(_workspace, created.Value.Session, selectSession: true);
        }
        finally
        {
            _cacheGate.Release();
        }

        AgentWorkspaceStateLog.SessionCached(logger, created.Value.Session.Id, created.Value.Session.PrimaryAgentId);

        return created;
    }

    public async ValueTask<Result<ProviderStatusDescriptor>> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        var provider = await agentSessionService.UpdateProviderAsync(command, cancellationToken);
        if (provider.IsFailed)
        {
            return provider;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is not null)
            {
                var providers = _workspace.Providers
                    .Select(existing => existing.Kind == provider.Value.Kind ? provider.Value : existing)
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

        AgentWorkspaceStateLog.ProviderCached(logger, provider.Value.Kind, provider.Value.IsEnabled);

        return provider;
    }

    public async ValueTask<Result<OperatorPreferencesSnapshot>> UpdateComposerSendBehaviorAsync(
        UpdateComposerSendBehaviorCommand command,
        CancellationToken cancellationToken)
    {
        var preferences = await agentSessionService.UpdateComposerSendBehaviorAsync(command, cancellationToken);
        if (preferences.IsFailed)
        {
            return preferences;
        }

        await _cacheGate.WaitAsync(cancellationToken);
        try
        {
            if (_workspace is not null)
            {
                _workspace = _workspace with
                {
                    Preferences = preferences.Value,
                };
            }
        }
        finally
        {
            _cacheGate.Release();
        }

        AgentWorkspaceStateLog.WorkspaceRefreshed(
            logger,
            _workspace?.Sessions.Count ?? 0,
            _workspace?.Agents.Count ?? 0,
            _workspace?.Providers.Count ?? 0);

        return preferences;
    }

    public async IAsyncEnumerable<Result<SessionStreamEntry>> SendMessageAsync(
        SendSessionMessageCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _ = await GetSessionAsync(command.SessionId, cancellationToken);

        await foreach (var entry in agentSessionService.SendMessageAsync(command, cancellationToken))
        {
            if (entry.IsFailed)
            {
                yield return entry;
                yield break;
            }

            await _cacheGate.WaitAsync(cancellationToken);
            try
            {
                if (_sessions.TryGetValue(command.SessionId, out var cachedSession))
                {
                    var updatedSession = cachedSession with
                    {
                        Entries = UpsertEntries(cachedSession.Entries, entry.Value),
                    };
                    _sessions[command.SessionId] = updatedSession;
                    _workspace = MergeWorkspaceSession(
                        _workspace,
                        UpdateSessionPreview(updatedSession.Session, entry.Value),
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

    private async ValueTask<Result<AgentWorkspaceSnapshot>> LoadWorkspaceAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (forceRefresh)
        {
            await providerStatusCache.RefreshAsync(cancellationToken);
        }

        var workspace = await agentSessionService.GetWorkspaceAsync(cancellationToken);
        if (workspace.IsFailed)
        {
            return workspace;
        }

        _workspace = workspace.Value;
        AgentWorkspaceStateLog.WorkspaceRefreshed(
            logger,
            workspace.Value.Sessions.Count,
            workspace.Value.Agents.Count,
            workspace.Value.Providers.Count);
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
