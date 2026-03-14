using DotPilot.Core.Features.AgentSessions;
using DotPilot.Core.Features.ControlPlaneDomain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotPilot.Runtime.Features.AgentSessions;

internal sealed class AgentSessionService(
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    AgentProviderStatusCache providerStatusCache,
    AgentRuntimeConversationFactory runtimeConversationFactory,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<AgentSessionService> logger)
    : IAgentSessionService, IDisposable
{
    private const string NotYetImplementedFormat = "{0} live CLI execution is not wired yet in this slice.";
    private const string SessionReadyText = "Session created. Send the first message to start the workflow.";
    private const string UserAuthor = "You";
    private const string ToolAuthor = "Tool";
    private const string StatusAuthor = "System";
    private const string DisabledProviderSendText = "The provider for this agent is disabled. Re-enable it in settings before sending.";
    private const string DebugToolStartText = "Preparing local debug workflow.";
    private const string DebugToolDoneText = "Debug workflow finished.";
    private const string ToolAccentLabel = "tool";
    private const string StatusAccentLabel = "status";
    private const string ErrorAccentLabel = "error";
    private static readonly System.Text.CompositeFormat NotYetImplementedCompositeFormat =
        System.Text.CompositeFormat.Parse(NotYetImplementedFormat);
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public async ValueTask<AgentWorkspaceSnapshot> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agents = await dbContext.AgentProfiles
            .OrderBy(record => record.Name)
            .ToListAsync(cancellationToken);
        var sessions = (await dbContext.Sessions
                .ToListAsync(cancellationToken))
            .OrderByDescending(record => record.UpdatedAt)
            .ToList();
        var entries = (await dbContext.SessionEntries
                .ToListAsync(cancellationToken))
            .OrderBy(record => record.Timestamp)
            .ToList();

        var agentsById = agents.ToDictionary(record => record.Id);
        var sessionItems = sessions
            .Select(record => MapSessionListItem(record, agentsById, entries))
            .ToArray();
        var providers = await providerStatusCache.GetSnapshotAsync(cancellationToken);

        AgentSessionServiceLog.WorkspaceLoaded(
            logger,
            sessionItems.Length,
            agents.Count,
            providers.Count);

        return new AgentWorkspaceSnapshot(
            sessionItems,
            agents.Select(MapAgentSummary).ToArray(),
            providers,
            sessionItems.Length > 0 ? sessionItems[0].Id : null);
    }

    public async ValueTask<SessionTranscriptSnapshot?> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Sessions
            .FirstOrDefaultAsync(record => record.Id == sessionId.Value, cancellationToken);
        if (session is null)
        {
            AgentSessionServiceLog.SessionNotFound(logger, sessionId);
            return null;
        }

        var agents = await dbContext.AgentProfiles
            .Where(record => record.Id == session.PrimaryAgentProfileId)
            .ToListAsync(cancellationToken);
        var agentsById = agents.ToDictionary(record => record.Id);
        var entries = (await dbContext.SessionEntries
                .Where(record => record.SessionId == sessionId.Value)
                .ToListAsync(cancellationToken))
            .OrderBy(record => record.Timestamp)
            .ToList();

        var snapshot = new SessionTranscriptSnapshot(
            MapSessionListItem(session, agentsById, entries),
            entries.Select(MapEntry).ToArray(),
            agents.Select(MapAgentSummary).ToArray());

        AgentSessionServiceLog.SessionLoaded(
            logger,
            sessionId,
            snapshot.Entries.Count,
            snapshot.Participants.Count);

        return snapshot;
    }

    public async ValueTask<AgentProfileSummary> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        var agentName = command.Name.Trim();
        var modelName = command.ModelName.Trim();
        var systemPrompt = command.SystemPrompt.Trim();
        AgentSessionServiceLog.AgentCreationStarted(logger, agentName, command.ProviderKind, command.Role);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var providers = await providerStatusCache.GetSnapshotAsync(cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);
            if (!provider.CanCreateAgents)
            {
                throw new InvalidOperationException(provider.StatusSummary);
            }

            var createdAt = timeProvider.GetUtcNow();
            var record = new AgentProfileRecord
            {
                Id = Guid.CreateVersion7(),
                Name = agentName,
                Role = (int)command.Role,
                ProviderKind = (int)command.ProviderKind,
                ModelName = modelName,
                SystemPrompt = systemPrompt,
                CapabilitiesJson = SerializeCapabilities(command.Capabilities),
                CreatedAt = createdAt,
            };

            dbContext.AgentProfiles.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
            await UpsertAgentGrainAsync(MapAgentDescriptor(record));

            AgentSessionServiceLog.AgentCreated(
                logger,
                record.Id,
                record.Name,
                command.ProviderKind);

            return MapAgentSummary(record);
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.AgentCreationFailed(logger, exception, agentName, command.ProviderKind);
            throw;
        }
    }

    public async ValueTask<SessionTranscriptSnapshot> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        var sessionTitle = command.Title.Trim();
        AgentSessionServiceLog.SessionCreationStarted(logger, sessionTitle, command.AgentProfileId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var agent = await dbContext.AgentProfiles
            .FirstAsync(record => record.Id == command.AgentProfileId.Value, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var sessionId = SessionId.New();
        var session = new SessionRecord
        {
            Id = sessionId.Value,
            Title = sessionTitle,
            PrimaryAgentProfileId = agent.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.Sessions.Add(session);
        dbContext.SessionEntries.Add(CreateEntryRecord(sessionId, SessionStreamEntryKind.Status, StatusAuthor, SessionReadyText, now, accentLabel: StatusAccentLabel));
        await dbContext.SaveChangesAsync(cancellationToken);
        await runtimeConversationFactory.InitializeAsync(agent, sessionId, cancellationToken);
        await UpsertSessionGrainAsync(session);

        AgentSessionServiceLog.SessionCreated(logger, sessionId, session.Title, command.AgentProfileId);

        return await GetSessionAsync(sessionId, cancellationToken) ??
            throw new InvalidOperationException("Created session could not be reloaded.");
    }

    public async ValueTask<ProviderStatusDescriptor> UpdateProviderAsync(
        UpdateProviderPreferenceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var record = await dbContext.ProviderPreferences
                .FirstOrDefaultAsync(preference => preference.ProviderKind == (int)command.ProviderKind, cancellationToken);
            if (record is null)
            {
                record = new ProviderPreferenceRecord
                {
                    ProviderKind = (int)command.ProviderKind,
                };
                dbContext.ProviderPreferences.Add(record);
            }

            record.IsEnabled = command.IsEnabled;
            record.UpdatedAt = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);

            var providers = await providerStatusCache.RefreshAsync(cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);

            AgentSessionServiceLog.ProviderPreferenceUpdated(logger, command.ProviderKind, command.IsEnabled);

            return provider;
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.ProviderPreferenceUpdateFailed(
                logger,
                exception,
                command.ProviderKind,
                command.IsEnabled);
            throw;
        }
    }

    public async IAsyncEnumerable<SessionStreamEntry> SendMessageAsync(
        SendSessionMessageCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Sessions
            .FirstAsync(record => record.Id == command.SessionId.Value, cancellationToken);
        var agent = await dbContext.AgentProfiles
            .FirstAsync(record => record.Id == session.PrimaryAgentProfileId, cancellationToken);
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)agent.ProviderKind);
        var runtimeConversation = await runtimeConversationFactory.LoadOrCreateAsync(agent, command.SessionId, cancellationToken);
        var now = timeProvider.GetUtcNow();

        AgentSessionServiceLog.SendStarted(
            logger,
            command.SessionId,
            agent.Id,
            providerProfile.Kind);

        var userEntry = CreateEntryRecord(command.SessionId, SessionStreamEntryKind.UserMessage, UserAuthor, command.Message.Trim(), now);
        dbContext.SessionEntries.Add(userEntry);
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        yield return MapEntry(userEntry);

        var statusEntry = CreateEntryRecord(
            command.SessionId,
            SessionStreamEntryKind.Status,
            StatusAuthor,
            $"Running {agent.Name} with {providerProfile.DisplayName}.",
            timeProvider.GetUtcNow(),
            accentLabel: StatusAccentLabel);
        yield return MapEntry(statusEntry);

        var providerStatuses = await providerStatusCache.GetSnapshotAsync(cancellationToken);
        var providerStatus = providerStatuses.First(status => status.Kind == providerProfile.Kind);

        if (!providerStatus.IsEnabled)
        {
            AgentSessionServiceLog.SendBlockedDisabled(logger, command.SessionId, providerProfile.Kind);
            var disabledEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.Error,
                StatusAuthor,
                DisabledProviderSendText,
                timeProvider.GetUtcNow(),
                accentLabel: ErrorAccentLabel);
            dbContext.SessionEntries.Add(disabledEntry);
            session.UpdatedAt = disabledEntry.Timestamp;
            await dbContext.SaveChangesAsync(cancellationToken);

            yield return MapEntry(disabledEntry);
            yield break;
        }

        if (providerProfile.Kind is not AgentProviderKind.Debug)
        {
            AgentSessionServiceLog.SendBlockedNotWired(logger, command.SessionId, providerProfile.Kind);
            var notImplementedEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.Error,
                StatusAuthor,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    NotYetImplementedCompositeFormat,
                    providerProfile.DisplayName),
                timeProvider.GetUtcNow(),
                accentLabel: ErrorAccentLabel);
            dbContext.SessionEntries.Add(notImplementedEntry);
            session.UpdatedAt = notImplementedEntry.Timestamp;
            await dbContext.SaveChangesAsync(cancellationToken);

            yield return MapEntry(notImplementedEntry);
            yield break;
        }

        var toolStartEntry = CreateEntryRecord(
            command.SessionId,
            SessionStreamEntryKind.ToolStarted,
            ToolAuthor,
            DebugToolStartText,
            timeProvider.GetUtcNow(),
            agentProfileId: new AgentProfileId(agent.Id),
            accentLabel: ToolAccentLabel);
        dbContext.SessionEntries.Add(toolStartEntry);
        await dbContext.SaveChangesAsync(cancellationToken);
        yield return MapEntry(toolStartEntry);

        string? streamedMessageId = null;
        var accumulated = new System.Text.StringBuilder();

        await using var updateEnumerator = runtimeConversation.Agent.RunStreamingAsync(
                command.Message.Trim(),
                runtimeConversation.Session,
                options: null,
                cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            Microsoft.Agents.AI.AgentResponseUpdate update;
            try
            {
                if (!await updateEnumerator.MoveNextAsync())
                {
                    break;
                }

                update = updateEnumerator.Current;
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                throw;
            }

            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }

            streamedMessageId ??= string.IsNullOrWhiteSpace(update.MessageId)
                ? Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
                : update.MessageId;
            accumulated.Append(update.Text);
            yield return new SessionStreamEntry(
                streamedMessageId,
                command.SessionId,
                SessionStreamEntryKind.AssistantMessage,
                agent.Name,
                accumulated.ToString(),
                update.CreatedAt ?? timeProvider.GetUtcNow(),
                new AgentProfileId(agent.Id));
        }

        SessionStreamEntry toolDoneStreamEntry;
        try
        {
            await runtimeConversationFactory.SaveAsync(runtimeConversation, command.SessionId, cancellationToken);

            var assistantEntry = new SessionEntryRecord
            {
                Id = streamedMessageId ?? Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                SessionId = command.SessionId.Value,
                AgentProfileId = agent.Id,
                Kind = (int)SessionStreamEntryKind.AssistantMessage,
                Author = agent.Name,
                Text = accumulated.ToString(),
                Timestamp = timeProvider.GetUtcNow(),
            };
            var toolDoneEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.ToolCompleted,
                ToolAuthor,
                DebugToolDoneText,
                timeProvider.GetUtcNow(),
                agentProfileId: new AgentProfileId(agent.Id),
                accentLabel: ToolAccentLabel);

            dbContext.SessionEntries.Add(assistantEntry);
            dbContext.SessionEntries.Add(toolDoneEntry);
            session.UpdatedAt = assistantEntry.Timestamp;
            await dbContext.SaveChangesAsync(cancellationToken);

            AgentSessionServiceLog.SendCompleted(logger, command.SessionId, agent.Id, accumulated.Length);
            toolDoneStreamEntry = MapEntry(toolDoneEntry);
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
            throw;
        }

        yield return toolDoneStreamEntry;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            AgentSessionServiceLog.InitializationStarted(logger);
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            _initialized = true;
            AgentSessionServiceLog.InitializationCompleted(logger);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private static SessionEntryRecord CreateEntryRecord(
        SessionId sessionId,
        SessionStreamEntryKind kind,
        string author,
        string text,
        DateTimeOffset timestamp,
        AgentProfileId? agentProfileId = null,
        string? accentLabel = null)
    {
        return new SessionEntryRecord
        {
            Id = Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
            SessionId = sessionId.Value,
            AgentProfileId = agentProfileId?.Value,
            Kind = (int)kind,
            Author = author,
            Text = text,
            Timestamp = timestamp,
            AccentLabel = accentLabel,
        };
    }

    private static SessionStreamEntry MapEntry(SessionEntryRecord record)
    {
        return new SessionStreamEntry(
            record.Id,
            new SessionId(record.SessionId),
            (SessionStreamEntryKind)record.Kind,
            record.Author,
            record.Text,
            record.Timestamp,
            record.AgentProfileId is Guid agentProfileId ? new AgentProfileId(agentProfileId) : null,
            record.AccentLabel);
    }

    private static SessionListItem MapSessionListItem(
        SessionRecord record,
        Dictionary<Guid, AgentProfileRecord> agentsById,
        IReadOnlyList<SessionEntryRecord> entries)
    {
        var agent = agentsById[record.PrimaryAgentProfileId];
        var preview = entries
            .Where(entry => entry.SessionId == record.Id)
            .OrderByDescending(entry => entry.Timestamp)
            .Select(entry => entry.Text)
            .FirstOrDefault() ?? string.Empty;
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)agent.ProviderKind);

        return new SessionListItem(
            new SessionId(record.Id),
            record.Title,
            preview,
            providerProfile.DisplayName,
            record.UpdatedAt,
            new AgentProfileId(agent.Id),
            agent.Name,
            providerProfile.DisplayName);
    }

    private static AgentProfileSummary MapAgentSummary(AgentProfileRecord record)
    {
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)record.ProviderKind);
        return new AgentProfileSummary(
            new AgentProfileId(record.Id),
            record.Name,
            (AgentRoleKind)record.Role,
            (AgentProviderKind)record.ProviderKind,
            providerProfile.DisplayName,
            record.ModelName,
            record.SystemPrompt,
            DeserializeCapabilities(record.CapabilitiesJson),
            record.CreatedAt);
    }

    private static AgentProfileDescriptor MapAgentDescriptor(AgentProfileRecord record)
    {
        var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)record.ProviderKind);
        return new AgentProfileDescriptor
        {
            Id = new AgentProfileId(record.Id),
            Name = record.Name,
            Role = (AgentRoleKind)record.Role,
            ProviderId = AgentSessionDeterministicIdentity.CreateProviderId(providerProfile.CommandName),
            ModelRuntimeId = null,
            Tags = DeserializeCapabilities(record.CapabilitiesJson).ToArray(),
        };
    }

    private static string SerializeCapabilities(IReadOnlyList<string> capabilities)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            capabilities.ToArray(),
            AgentSessionJsonSerializerContext.Default.StringArray);
    }

    private static string[] DeserializeCapabilities(string capabilitiesJson)
    {
        return System.Text.Json.JsonSerializer.Deserialize(
            capabilitiesJson,
            AgentSessionJsonSerializerContext.Default.StringArray) ?? [];
    }

    private async Task UpsertAgentGrainAsync(AgentProfileDescriptor descriptor)
    {
        var grainFactory = serviceProvider.GetService<IGrainFactory>();
        if (grainFactory is null)
        {
            return;
        }

        await grainFactory
            .GetGrain<IAgentProfileGrain>(descriptor.Id.ToString())
            .UpsertAsync(descriptor);
    }

    private async Task UpsertSessionGrainAsync(SessionRecord record)
    {
        var grainFactory = serviceProvider.GetService<IGrainFactory>();
        if (grainFactory is null)
        {
            return;
        }

        await grainFactory
            .GetGrain<ISessionGrain>(record.Id.ToString("N", System.Globalization.CultureInfo.InvariantCulture))
            .UpsertAsync(
                new SessionDescriptor
                {
                    Id = new SessionId(record.Id),
                    WorkspaceId = WorkspaceId.New(),
                    Title = record.Title,
                    Phase = SessionPhase.Execute,
                    ApprovalState = ApprovalState.NotRequired,
                    FleetId = null,
                    AgentProfileIds = [new AgentProfileId(record.PrimaryAgentProfileId)],
                    CreatedAt = record.CreatedAt,
                    UpdatedAt = record.UpdatedAt,
                });
    }

    public void Dispose()
    {
        _initializationGate.Dispose();
    }
}
