using DotPilot.Core.AgentBuilder;
using DotPilot.Core.Providers;
using ManagedCode.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed partial class AgentSessionService(
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    AgentExecutionLoggingMiddleware executionLoggingMiddleware,
    ISessionActivityMonitor sessionActivityMonitor,
    IAgentProviderStatusReader providerStatusReader,
    AgentRuntimeConversationFactory runtimeConversationFactory,
    LocalAgentSessionStateStore sessionStateStore,
    LocalAgentChatHistoryStore chatHistoryStore,
    AgentSessionStorageOptions storageOptions,
    TimeProvider timeProvider,
    ILogger<AgentSessionService> logger)
    : IAgentSessionService, IDisposable
{
    private const string SessionReadyTextFormat = "Session started with {0} on {1}. Send a message when ready.";
    private const string SessionActiveCloseText = "The session is still active. Wait for the current run to finish before closing it.";
    private const string UserAuthor = "You";
    private const string ToolAuthor = "Tool";
    private const string StatusAuthor = "System";
    private const string DisabledProviderSendText = "The provider for this agent is disabled. Re-enable it in settings before sending.";
    private const string ToolAccentLabel = "tool";
    private const string StatusAccentLabel = "status";
    private const string ErrorAccentLabel = "error";
    private static readonly System.Text.CompositeFormat SessionReadyTextCompositeFormat =
        System.Text.CompositeFormat.Parse(SessionReadyTextFormat);
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public async ValueTask<Result<AgentWorkspaceSnapshot>> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await LoadWorkspaceAsync(forceRefreshProviders: false, cancellationToken);
    }

    public async ValueTask<Result<AgentWorkspaceSnapshot>> RefreshWorkspaceAsync(CancellationToken cancellationToken)
    {
        return await LoadWorkspaceAsync(forceRefreshProviders: true, cancellationToken);
    }

    private async ValueTask<Result<AgentWorkspaceSnapshot>> LoadWorkspaceAsync(
        bool forceRefreshProviders,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var agents = await dbContext.AgentProfiles
                .ToListAsync(cancellationToken);
            agents = OrderAgents(agents);
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
            var providers = await GetProviderStatusesAsync(forceRefreshProviders, cancellationToken);

            AgentSessionServiceLog.WorkspaceLoaded(
                logger,
                sessionItems.Length,
                agents.Count,
                providers.Count);

            return Result<AgentWorkspaceSnapshot>.Succeed(new AgentWorkspaceSnapshot(
                sessionItems,
                agents.Select(MapAgentSummary).ToArray(),
                providers,
                sessionItems.Length > 0 ? sessionItems[0].Id : null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.WorkspaceLoadFailed(logger, exception);
            return Result<AgentWorkspaceSnapshot>.Fail(exception);
        }
    }

    public async ValueTask<Result<SessionTranscriptSnapshot>> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await EnsureInitializedAsync(cancellationToken);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var session = await dbContext.Sessions
                .FirstOrDefaultAsync(record => record.Id == sessionId.Value, cancellationToken);
            if (session is null)
            {
                AgentSessionServiceLog.SessionNotFound(logger, sessionId);
                return Result<SessionTranscriptSnapshot>.FailNotFound($"Session '{sessionId}' was not found.");
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

            return Result<SessionTranscriptSnapshot>.Succeed(snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SessionLoadFailed(logger, exception, sessionId);
            return Result<SessionTranscriptSnapshot>.Fail(exception);
        }
    }

    public async ValueTask<Result<AgentProfileSummary>> CreateAgentAsync(
        CreateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        var agentName = command.Name.Trim();
        var modelName = command.ModelName.Trim();
        var systemPrompt = command.SystemPrompt.Trim();
        var description = ResolveAgentDescription(command.Description, systemPrompt);
        AgentSessionServiceLog.AgentCreationStarted(logger, agentName, command.ProviderKind);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var providers = await GetProviderStatusesAsync(forceRefresh: false, cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);
            if (!provider.CanCreateAgents)
            {
                return Result<AgentProfileSummary>.FailForbidden(provider.StatusSummary);
            }

            var createdAt = timeProvider.GetUtcNow();
            var record = new AgentProfileRecord
            {
                Id = Guid.CreateVersion7(),
                Name = agentName,
                Description = description,
                Role = AgentProfileSchemaDefaults.DefaultRole,
                ProviderKind = (int)command.ProviderKind,
                ModelName = modelName,
                SystemPrompt = systemPrompt,
                CapabilitiesJson = AgentProfileSchemaDefaults.EmptyCapabilitiesJson,
                CreatedAt = createdAt,
            };

            dbContext.AgentProfiles.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);

            AgentSessionServiceLog.AgentCreated(
                logger,
                record.Id,
                record.Name,
                command.ProviderKind);

            return Result<AgentProfileSummary>.Succeed(MapAgentSummary(record));
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.AgentCreationFailed(logger, exception, agentName, command.ProviderKind);
            return Result<AgentProfileSummary>.Fail(exception);
        }
    }

    public async ValueTask<Result<AgentProfileSummary>> UpdateAgentAsync(
        UpdateAgentProfileCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        var agentName = command.Name.Trim();
        var modelName = command.ModelName.Trim();
        var systemPrompt = command.SystemPrompt.Trim();
        var description = ResolveAgentDescription(command.Description, systemPrompt);
        AgentSessionServiceLog.AgentUpdateStarted(logger, command.AgentId, agentName, command.ProviderKind);

        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var record = await dbContext.AgentProfiles
                .FirstOrDefaultAsync(agent => agent.Id == command.AgentId.Value, cancellationToken);
            if (record is null)
            {
                return Result<AgentProfileSummary>.FailNotFound($"Agent '{command.AgentId}' was not found.");
            }

            var providers = await GetProviderStatusesAsync(forceRefresh: false, cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);
            if (!provider.CanCreateAgents)
            {
                return Result<AgentProfileSummary>.FailForbidden(provider.StatusSummary);
            }

            record.Name = agentName;
            record.Description = description;
            record.ProviderKind = (int)command.ProviderKind;
            record.ModelName = modelName;
            record.SystemPrompt = systemPrompt;

            await dbContext.SaveChangesAsync(cancellationToken);

            AgentSessionServiceLog.AgentUpdated(
                logger,
                record.Id,
                record.Name,
                command.ProviderKind);

            return Result<AgentProfileSummary>.Succeed(MapAgentSummary(record));
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.AgentUpdateFailed(logger, exception, command.AgentId, agentName, command.ProviderKind);
            return Result<AgentProfileSummary>.Fail(exception);
        }
    }

    public async ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        AgentProfileRecord? agentRecord = null;
        SessionId sessionId = default;
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var sessionTitle = command.Title.Trim();
            AgentSessionServiceLog.SessionCreationStarted(logger, sessionTitle, command.AgentProfileId);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            agentRecord = await dbContext.AgentProfiles
                .FirstOrDefaultAsync(record => record.Id == command.AgentProfileId.Value, cancellationToken);
            if (agentRecord is null)
            {
                return Result<SessionTranscriptSnapshot>.FailNotFound($"Agent '{command.AgentProfileId}' was not found.");
            }

            var providerResult = await GetValidatedSessionProviderAsync(
                (AgentProviderKind)agentRecord.ProviderKind,
                cancellationToken);
            if (providerResult.IsFailed)
            {
                return Result<SessionTranscriptSnapshot>.Fail(providerResult.Problem!);
            }
            var provider = providerResult.Value!;

            var now = timeProvider.GetUtcNow();
            sessionId = SessionId.New();
            await runtimeConversationFactory.InitializeAsync(agentRecord, sessionId, cancellationToken);
            var session = new SessionRecord
            {
                Id = sessionId.Value,
                Title = sessionTitle,
                PrimaryAgentProfileId = agentRecord.Id,
                CreatedAt = now,
                UpdatedAt = now,
            };

            dbContext.Sessions.Add(session);
            dbContext.SessionEntries.Add(CreateEntryRecord(
                sessionId,
                SessionStreamEntryKind.Status,
                StatusAuthor,
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    SessionReadyTextCompositeFormat,
                    agentRecord.Name,
                    provider.DisplayName),
                now,
                accentLabel: StatusAccentLabel));
            await dbContext.SaveChangesAsync(cancellationToken);

            AgentSessionServiceLog.SessionCreated(logger, sessionId, session.Title, command.AgentProfileId);

            var reloaded = await GetSessionAsync(sessionId, cancellationToken);
            return reloaded.IsSuccess
                ? reloaded
                : Result<SessionTranscriptSnapshot>.Fail("SessionCreationFailed", "Created session could not be reloaded.");
        }
        catch (Exception exception)
        {
            if (agentRecord is not null && sessionId != default)
            {
                await TryCloseSessionRuntimeAsync(agentRecord, sessionId);
            }

            AgentSessionServiceLog.SessionCreationFailed(logger, exception, command.Title, command.AgentProfileId);
            return Result<SessionTranscriptSnapshot>.Fail(exception);
        }
    }

    public async ValueTask<Result<AgentWorkspaceSnapshot>> CloseSessionAsync(
        CloseSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            AgentSessionServiceLog.SessionCloseStarted(logger, command.SessionId);

            if (sessionActivityMonitor.Current.ActiveSessions.Any(descriptor => descriptor.SessionId == command.SessionId))
            {
                AgentSessionServiceLog.SessionCloseBlockedActive(logger, command.SessionId);
                return Result<AgentWorkspaceSnapshot>.FailForbidden(SessionActiveCloseText);
            }

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var session = await dbContext.Sessions
                .FirstOrDefaultAsync(record => record.Id == command.SessionId.Value, cancellationToken);
            if (session is null)
            {
                return Result<AgentWorkspaceSnapshot>.FailNotFound($"Session '{command.SessionId}' was not found.");
            }

            var agentRecord = await dbContext.AgentProfiles
                .FirstOrDefaultAsync(record => record.Id == session.PrimaryAgentProfileId, cancellationToken);
            await runtimeConversationFactory.CloseAsync(agentRecord, command.SessionId, cancellationToken);

            var entries = await dbContext.SessionEntries
                .Where(record => record.SessionId == command.SessionId.Value)
                .ToListAsync(cancellationToken);
            dbContext.SessionEntries.RemoveRange(entries);
            dbContext.Sessions.Remove(session);
            await dbContext.SaveChangesAsync(cancellationToken);

            AgentSessionServiceLog.SessionClosed(logger, command.SessionId);
            return await LoadWorkspaceAsync(forceRefreshProviders: false, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SessionCloseFailed(logger, exception, command.SessionId);
            return Result<AgentWorkspaceSnapshot>.Fail(exception);
        }
    }

    public async ValueTask<Result<ProviderStatusDescriptor>> UpdateProviderAsync(
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

            InvalidateProviderStatusSnapshot();
            var providers = await GetProviderStatusesAsync(forceRefresh: true, cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);

            AgentSessionServiceLog.ProviderPreferenceUpdated(logger, command.ProviderKind, command.IsEnabled);

            return Result<ProviderStatusDescriptor>.Succeed(provider);
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.ProviderPreferenceUpdateFailed(
                logger,
                exception,
                command.ProviderKind,
                command.IsEnabled);
            return Result<ProviderStatusDescriptor>.Fail(exception);
        }
    }

    public async ValueTask<Result<ProviderStatusDescriptor>> SetLocalModelPathAsync(
        SetLocalModelPathCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureInitializedAsync(cancellationToken);
        try
        {
            if (!command.ProviderKind.IsLocalModelProvider())
            {
                return Result<ProviderStatusDescriptor>.Fail(
                    "UnsupportedProvider",
                    $"{command.ProviderKind.GetDisplayName()} does not use a local model path.");
            }

            var localModelPath = command.LocalModelPath.Trim();
            if (string.IsNullOrWhiteSpace(localModelPath))
            {
                return Result<ProviderStatusDescriptor>.Fail(
                    "MissingModelPath",
                    "A local model path is required.");
            }

            var configuration = await LocalModelProviderCompatibilityReader.ReadAsync(
                command.ProviderKind,
                localModelPath,
                cancellationToken);
            if (!configuration.IsCompatible || string.IsNullOrWhiteSpace(configuration.NormalizedModelPath))
            {
                return Result<ProviderStatusDescriptor>.Fail(
                    configuration.FailureCode ?? "InvalidModelPath",
                    configuration.FailureMessage ?? command.ProviderKind.GetLocalModelMissingSummary());
            }

            var normalizedModelPath = configuration.NormalizedModelPath!;

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

            var updatedAt = timeProvider.GetUtcNow();
            record.LocalModelPath = normalizedModelPath;
            record.UpdatedAt = updatedAt;
            var localModelRecord = await dbContext.ProviderLocalModels
                .FirstOrDefaultAsync(
                    provider => provider.ProviderKind == (int)command.ProviderKind &&
                        provider.ModelPath == normalizedModelPath,
                    cancellationToken);
            if (localModelRecord is null)
            {
                dbContext.ProviderLocalModels.Add(new ProviderLocalModelRecord
                {
                    ProviderKind = (int)command.ProviderKind,
                    ModelPath = normalizedModelPath,
                    AddedAt = updatedAt,
                });
            }
            else
            {
                localModelRecord.AddedAt = updatedAt;
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            InvalidateProviderStatusSnapshot();
            var providers = await GetProviderStatusesAsync(forceRefresh: true, cancellationToken);
            var provider = providers.First(status => status.Kind == command.ProviderKind);
            AgentSessionServiceLog.LocalModelPathUpdated(logger, command.ProviderKind, normalizedModelPath);
            return Result<ProviderStatusDescriptor>.Succeed(provider);
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.LocalModelPathUpdateFailed(logger, exception, command.ProviderKind);
            return Result<ProviderStatusDescriptor>.Fail(exception);
        }
    }

    public async IAsyncEnumerable<Result<SessionStreamEntry>> SendMessageAsync(
        SendSessionMessageCommand command,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Result initializeResult;
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            initializeResult = Result.Succeed();
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, Guid.Empty);
            initializeResult = Result.Fail(exception);
        }

        if (initializeResult.IsFailed)
        {
            yield return Result<SessionStreamEntry>.Fail(initializeResult.Problem!);
            yield break;
        }

        LocalAgentSessionDbContext? dbContext = null;
        Result dbContextResult;
        try
        {
            dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContextResult = Result.Succeed();
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, Guid.Empty);
            dbContextResult = Result.Fail(exception);
        }

        if (dbContextResult.IsFailed || dbContext is null)
        {
            yield return Result<SessionStreamEntry>.Fail(dbContextResult.Problem!);
            yield break;
        }

        await using (dbContext)
        {
            Result<(SessionRecord Session, AgentProfileRecord Agent, AgentProviderKind ProviderKind, string ProviderDisplayName, DateTimeOffset Timestamp)> contextResult;
            try
            {
                var sessionRecord = await dbContext.Sessions
                    .FirstOrDefaultAsync(record => record.Id == command.SessionId.Value, cancellationToken);
                if (sessionRecord is null)
                {
                    contextResult = Result<(SessionRecord, AgentProfileRecord, AgentProviderKind, string, DateTimeOffset)>.FailNotFound(
                        $"Session '{command.SessionId}' was not found.");
                }
                else
                {
                    var agentRecord = await dbContext.AgentProfiles
                        .FirstOrDefaultAsync(record => record.Id == sessionRecord.PrimaryAgentProfileId, cancellationToken);
                    if (agentRecord is null)
                    {
                        contextResult = Result<(SessionRecord, AgentProfileRecord, AgentProviderKind, string, DateTimeOffset)>.FailNotFound(
                            $"Agent '{sessionRecord.PrimaryAgentProfileId}' was not found.");
                    }
                    else
                    {
                        var agentProviderKind = (AgentProviderKind)agentRecord.ProviderKind;
                        contextResult = Result<(SessionRecord, AgentProfileRecord, AgentProviderKind, string, DateTimeOffset)>.Succeed((
                            sessionRecord,
                            agentRecord,
                            agentProviderKind,
                            agentProviderKind.GetDisplayName(),
                            timeProvider.GetUtcNow()));
                    }
                }
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, Guid.Empty);
                contextResult = Result<(SessionRecord, AgentProfileRecord, AgentProviderKind, string, DateTimeOffset)>.Fail(exception);
            }

            if (contextResult.IsFailed)
            {
                yield return Result<SessionStreamEntry>.Fail(contextResult.Problem!);
                yield break;
            }

            var (session, agent, providerKind, providerDisplayName, now) = contextResult.Value;
            AgentSessionServiceLog.SendStarted(
                logger,
                command.SessionId,
                agent.Id,
                providerKind);

            Result<SessionEntryRecord> userEntryResult;
            try
            {
                var userEntry = CreateEntryRecord(command.SessionId, SessionStreamEntryKind.UserMessage, UserAuthor, command.Message.Trim(), now);
                dbContext.SessionEntries.Add(userEntry);
                session.UpdatedAt = now;
                await dbContext.SaveChangesAsync(cancellationToken);
                userEntryResult = Result<SessionEntryRecord>.Succeed(userEntry);
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                userEntryResult = Result<SessionEntryRecord>.Fail(exception);
            }

            if (userEntryResult.IsFailed)
            {
                yield return Result<SessionStreamEntry>.Fail(userEntryResult.Problem!);
                yield break;
            }

            yield return Result<SessionStreamEntry>.Succeed(MapEntry(userEntryResult.Value));

            var statusEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.Status,
                StatusAuthor,
                $"Running {agent.Name} with {providerDisplayName}.",
                timeProvider.GetUtcNow(),
                accentLabel: StatusAccentLabel);
            Result<SessionStreamEntry> statusEntryResult;
            try
            {
                dbContext.SessionEntries.Add(statusEntry);
                session.UpdatedAt = statusEntry.Timestamp;
                await dbContext.SaveChangesAsync(cancellationToken);
                statusEntryResult = Result<SessionStreamEntry>.Succeed(MapEntry(statusEntry));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                statusEntryResult = Result<SessionStreamEntry>.Fail(exception);
            }

            if (statusEntryResult.IsFailed)
            {
                yield return statusEntryResult;
                yield break;
            }

            yield return statusEntryResult;

            Result<ProviderStatusDescriptor> providerStatusResult;
            try
            {
                var providerStatuses = await GetProviderStatusesAsync(forceRefresh: false, cancellationToken);
                providerStatusResult = Result<ProviderStatusDescriptor>.Succeed(
                    providerStatuses.First(status => status.Kind == providerKind));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                providerStatusResult = Result<ProviderStatusDescriptor>.Fail(exception);
            }

            if (providerStatusResult.IsFailed)
            {
                var providerFailureEntryResult = await TryAppendSendFailureEntryAsync(
                    dbContext,
                    session,
                    command.SessionId,
                    agent.Id,
                    providerDisplayName,
                    providerStatusResult.Problem!,
                    cancellationToken);
                if (providerFailureEntryResult.IsSuccess)
                {
                    yield return providerFailureEntryResult;
                }

                yield return Result<SessionStreamEntry>.Fail(providerStatusResult.Problem!);
                yield break;
            }

            var providerStatus = providerStatusResult.Value;
            if (!providerStatus.IsEnabled)
            {
                AgentSessionServiceLog.SendBlockedDisabled(logger, command.SessionId, providerKind);
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

                yield return Result<SessionStreamEntry>.Succeed(MapEntry(disabledEntry));
                yield break;
            }

            if (!providerStatus.CanCreateAgents)
            {
                var unavailableEntry = CreateEntryRecord(
                    command.SessionId,
                    SessionStreamEntryKind.Error,
                    StatusAuthor,
                    providerStatus.StatusSummary,
                    timeProvider.GetUtcNow(),
                    accentLabel: ErrorAccentLabel);
                dbContext.SessionEntries.Add(unavailableEntry);
                session.UpdatedAt = unavailableEntry.Timestamp;
                await dbContext.SaveChangesAsync(cancellationToken);

                yield return Result<SessionStreamEntry>.Succeed(MapEntry(unavailableEntry));
                yield break;
            }

            using var liveActivity = sessionActivityMonitor.BeginActivity(
                new SessionActivityDescriptor(
                    command.SessionId,
                    session.Title,
                    new AgentProfileId(agent.Id),
                    agent.Name,
                    providerDisplayName));

            Result<RuntimeConversationContext> runtimeConversationResult;
            try
            {
                runtimeConversationResult = Result<RuntimeConversationContext>.Succeed(
                    await runtimeConversationFactory.LoadOrCreateAsync(agent, command.SessionId, cancellationToken));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                runtimeConversationResult = Result<RuntimeConversationContext>.Fail(exception);
            }

            if (runtimeConversationResult.IsFailed)
            {
                var runtimeFailureEntryResult = await TryAppendSendFailureEntryAsync(
                    dbContext,
                    session,
                    command.SessionId,
                    agent.Id,
                    providerDisplayName,
                    runtimeConversationResult.Problem!,
                    cancellationToken);
                if (runtimeFailureEntryResult.IsSuccess)
                {
                    yield return runtimeFailureEntryResult;
                }

                yield return Result<SessionStreamEntry>.Fail(runtimeConversationResult.Problem!);
                yield break;
            }

            var runtimeConversation = runtimeConversationResult.Value;
            var toolStartEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.ToolStarted,
                ToolAuthor,
                CreateToolStartText(providerKind),
                timeProvider.GetUtcNow(),
                agentProfileId: new AgentProfileId(agent.Id),
                accentLabel: ToolAccentLabel);
            Result<SessionStreamEntry> toolStartEntryResult;
            try
            {
                dbContext.SessionEntries.Add(toolStartEntry);
                session.UpdatedAt = toolStartEntry.Timestamp;
                await dbContext.SaveChangesAsync(cancellationToken);
                toolStartEntryResult = Result<SessionStreamEntry>.Succeed(MapEntry(toolStartEntry));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                toolStartEntryResult = Result<SessionStreamEntry>.Fail(exception);
            }

            if (toolStartEntryResult.IsFailed)
            {
                yield return toolStartEntryResult;
                yield break;
            }

            yield return toolStartEntryResult;

            string? streamedMessageId = null;
            SessionEntryRecord? streamedAssistantEntry = null;
            var accumulated = new System.Text.StringBuilder();
            var runConfiguration = executionLoggingMiddleware.CreateRunConfiguration(
                runtimeConversation.Descriptor,
                command.SessionId);
            AgentSessionServiceLog.SendRunPrepared(
                logger,
                command.SessionId,
                agent.Id,
                runConfiguration.Context.RunId,
                providerKind,
                agent.ModelName);

            IAsyncEnumerator<Microsoft.Agents.AI.AgentResponseUpdate>? updateEnumerator = null;
            Result<IAsyncEnumerator<Microsoft.Agents.AI.AgentResponseUpdate>> updateEnumeratorResult;
            try
            {
                updateEnumerator = runtimeConversation.Agent.RunStreamingAsync(
                        command.Message.Trim(),
                        runtimeConversation.Session,
                        runConfiguration.Options,
                        cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
                updateEnumeratorResult = Result<IAsyncEnumerator<Microsoft.Agents.AI.AgentResponseUpdate>>.Succeed(updateEnumerator);
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                updateEnumeratorResult = Result<IAsyncEnumerator<Microsoft.Agents.AI.AgentResponseUpdate>>.Fail(exception);
            }

            if (updateEnumeratorResult.IsFailed || updateEnumerator is null)
            {
                var streamStartFailureEntryResult = await TryAppendSendFailureEntryAsync(
                    dbContext,
                    session,
                    command.SessionId,
                    agent.Id,
                    providerDisplayName,
                    updateEnumeratorResult.Problem!,
                    cancellationToken);
                if (streamStartFailureEntryResult.IsSuccess)
                {
                    yield return streamStartFailureEntryResult;
                }

                yield return Result<SessionStreamEntry>.Fail(updateEnumeratorResult.Problem!);
                yield break;
            }

            await using (updateEnumerator)
            {
                while (true)
                {
                    Microsoft.Agents.AI.AgentResponseUpdate? update = null;
                    Result<bool> nextUpdateResult;
                    try
                    {
                        var hasNext = await updateEnumerator.MoveNextAsync();
                        if (hasNext)
                        {
                            update = updateEnumerator.Current;
                        }

                        nextUpdateResult = Result<bool>.Succeed(hasNext);
                    }
                    catch (Exception exception)
                    {
                        AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                        nextUpdateResult = Result<bool>.Fail(exception);
                    }

                    if (nextUpdateResult.IsFailed)
                    {
                        var streamFailureEntryResult = await TryAppendSendFailureEntryAsync(
                            dbContext,
                            session,
                            command.SessionId,
                            agent.Id,
                            providerDisplayName,
                            nextUpdateResult.Problem!,
                            cancellationToken);
                        if (streamFailureEntryResult.IsSuccess)
                        {
                            yield return streamFailureEntryResult;
                        }

                        yield return Result<SessionStreamEntry>.Fail(nextUpdateResult.Problem!);
                        yield break;
                    }

                    if (!nextUpdateResult.Value)
                    {
                        break;
                    }

                    if (string.IsNullOrEmpty(update?.Text))
                    {
                        continue;
                    }

                    streamedMessageId ??= string.IsNullOrWhiteSpace(update.MessageId)
                        ? Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
                        : update.MessageId;
                    accumulated.Append(update.Text);
                    Result<SessionStreamEntry> streamedEntryResult;
                    try
                    {
                        var timestamp = update.CreatedAt ?? timeProvider.GetUtcNow();
                        if (streamedAssistantEntry is null)
                        {
                            streamedAssistantEntry = new SessionEntryRecord
                            {
                                Id = streamedMessageId,
                                SessionId = command.SessionId.Value,
                                AgentProfileId = agent.Id,
                                Kind = (int)SessionStreamEntryKind.AssistantMessage,
                                Author = agent.Name,
                                Text = accumulated.ToString(),
                                Timestamp = timestamp,
                            };
                            dbContext.SessionEntries.Add(streamedAssistantEntry);
                        }
                        else
                        {
                            streamedAssistantEntry.Text = accumulated.ToString();
                            streamedAssistantEntry.Timestamp = timestamp;
                        }

                        session.UpdatedAt = timestamp;
                        await dbContext.SaveChangesAsync(cancellationToken);
                        streamedEntryResult = Result<SessionStreamEntry>.Succeed(MapEntry(streamedAssistantEntry));
                    }
                    catch (Exception exception)
                    {
                        AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                        streamedEntryResult = Result<SessionStreamEntry>.Fail(exception);
                    }

                    if (streamedEntryResult.IsFailed)
                    {
                        yield return streamedEntryResult;
                        yield break;
                    }

                    yield return streamedEntryResult;
                }
            }

            Result<SessionStreamEntry> completionResult;
            try
            {
                await runtimeConversationFactory.SaveAsync(runtimeConversation, command.SessionId, cancellationToken);

                if (streamedAssistantEntry is null)
                {
                    streamedAssistantEntry = new SessionEntryRecord
                    {
                        Id = streamedMessageId ?? Guid.CreateVersion7().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                        SessionId = command.SessionId.Value,
                        AgentProfileId = agent.Id,
                        Kind = (int)SessionStreamEntryKind.AssistantMessage,
                        Author = agent.Name,
                        Text = accumulated.ToString(),
                        Timestamp = timeProvider.GetUtcNow(),
                    };
                    dbContext.SessionEntries.Add(streamedAssistantEntry);
                }
                else
                {
                    streamedAssistantEntry.Text = accumulated.ToString();
                    streamedAssistantEntry.Timestamp = timeProvider.GetUtcNow();
                }

                var toolDoneEntry = CreateEntryRecord(
                    command.SessionId,
                    SessionStreamEntryKind.ToolCompleted,
                    ToolAuthor,
                    CreateToolDoneText(providerKind),
                    timeProvider.GetUtcNow(),
                    agentProfileId: new AgentProfileId(agent.Id),
                    accentLabel: ToolAccentLabel);

                dbContext.SessionEntries.Add(toolDoneEntry);
                session.UpdatedAt = toolDoneEntry.Timestamp;
                await dbContext.SaveChangesAsync(cancellationToken);

                AgentSessionServiceLog.SendCompleted(logger, command.SessionId, agent.Id, accumulated.Length);
                completionResult = Result<SessionStreamEntry>.Succeed(MapEntry(toolDoneEntry));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                completionResult = Result<SessionStreamEntry>.Fail(exception);
            }

            if (completionResult.IsFailed)
            {
                var completionFailureEntryResult = await TryAppendSendFailureEntryAsync(
                    dbContext,
                    session,
                    command.SessionId,
                    agent.Id,
                    providerDisplayName,
                    completionResult.Problem!,
                    cancellationToken);
                if (completionFailureEntryResult.IsSuccess)
                {
                    yield return completionFailureEntryResult;
                }
            }

            yield return completionResult;
        }
    }

    private async ValueTask<Result<ProviderStatusDescriptor>> GetValidatedSessionProviderAsync(
        AgentProviderKind providerKind,
        CancellationToken cancellationToken)
    {
        var providers = await GetProviderStatusesAsync(forceRefresh: false, cancellationToken);
        var provider = providers.First(status => status.Kind == providerKind);
        if (!provider.IsEnabled)
        {
            return Result<ProviderStatusDescriptor>.FailForbidden(DisabledProviderSendText);
        }

        return provider.CanCreateAgents
            ? Result<ProviderStatusDescriptor>.Succeed(provider)
            : Result<ProviderStatusDescriptor>.FailForbidden(provider.StatusSummary);
    }

    private async ValueTask TryCloseSessionRuntimeAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId)
    {
        try
        {
            await runtimeConversationFactory.CloseAsync(agentRecord, sessionId, CancellationToken.None);
        }
        catch
        {
        }
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
            await AgentProfileSchemaCompatibilityEnsurer.EnsureAsync(dbContext, cancellationToken);
            await EnsureDefaultProviderAndAgentAsync(dbContext, cancellationToken);
            _initialized = true;
            AgentSessionServiceLog.InitializationCompleted(logger);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private async Task EnsureDefaultProviderAndAgentAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var hasEnabledProvider = await dbContext.ProviderPreferences
            .AnyAsync(record => record.IsEnabled, cancellationToken);
        if (!hasEnabledProvider)
        {
            await EnsureProviderEnabledAsync(dbContext, AgentProviderKind.Debug, cancellationToken);
        }

        await NormalizeLegacyAgentProfilesAsync(dbContext, cancellationToken);

        var hasAgents = await dbContext.AgentProfiles.AnyAsync(cancellationToken);
        if (hasAgents)
        {
            return;
        }

        var providerKind = await ResolveSeedProviderKindAsync(dbContext, cancellationToken);
        if (providerKind == AgentProviderKind.Debug)
        {
            await EnsureProviderEnabledAsync(dbContext, providerKind, cancellationToken);
        }

        var record = new AgentProfileRecord
        {
            Id = Guid.CreateVersion7(),
            Name = AgentSessionDefaults.SystemAgentName,
            Description = AgentSessionDefaults.SystemAgentDescription,
            Role = AgentProfileSchemaDefaults.DefaultRole,
            ProviderKind = (int)providerKind,
            ModelName = AgentSessionDefaults.GetDefaultModel(providerKind),
            SystemPrompt = AgentSessionDefaults.SystemAgentPrompt,
            CapabilitiesJson = AgentProfileSchemaDefaults.EmptyCapabilitiesJson,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        dbContext.AgentProfiles.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        AgentSessionServiceLog.DefaultAgentSeeded(logger, record.Id, providerKind, record.ModelName);
    }

    private async Task EnsureProviderEnabledAsync(
        LocalAgentSessionDbContext dbContext,
        AgentProviderKind providerKind,
        CancellationToken cancellationToken)
    {
        var preference = await dbContext.ProviderPreferences
            .FirstOrDefaultAsync(
                record => record.ProviderKind == (int)providerKind,
                cancellationToken);

        if (preference is null)
        {
            preference = new ProviderPreferenceRecord
            {
                ProviderKind = (int)providerKind,
            };
            dbContext.ProviderPreferences.Add(preference);
        }

        if (preference.IsEnabled)
        {
            return;
        }

        preference.IsEnabled = true;
        preference.UpdatedAt = timeProvider.GetUtcNow();
        AgentSessionServiceLog.DefaultProviderEnabled(logger, providerKind);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async ValueTask<AgentProviderKind> ResolveSeedProviderKindAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var enabledProviderPreferences = await dbContext.ProviderPreferences
            .Where(record => record.IsEnabled)
            .ToArrayAsync(cancellationToken);
        var localModelsByProvider = (await dbContext.ProviderLocalModels
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .GroupBy(record => (AgentProviderKind)record.ProviderKind)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProviderLocalModelRecord>)group.ToArray());

        foreach (var preference in enabledProviderPreferences)
        {
            var providerKind = (AgentProviderKind)preference.ProviderKind;
            if (providerKind.IsBuiltIn())
            {
                continue;
            }

            if (providerKind.IsLocalModelProvider() &&
                (await LocalModelProviderConfigurationReader.ReadAsync(
                    providerKind,
                    localModelsByProvider.TryGetValue(providerKind, out var localModels)
                        ? localModels
                        : Array.Empty<ProviderLocalModelRecord>(),
                    preference.LocalModelPath,
                    cancellationToken).ConfigureAwait(false)).IsReady)
            {
                return providerKind;
            }

            if (!string.IsNullOrWhiteSpace(ResolveExecutablePath(providerKind.GetCommandName())))
            {
                return providerKind;
            }
        }

        return AgentProviderKind.Debug;
    }

    private static string? ResolveExecutablePath(string commandName)
    {
        if (OperatingSystem.IsBrowser())
        {
            return null;
        }

        var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var searchPath in searchPaths)
        {
            foreach (var candidate in EnumerateExecutableCandidates(searchPath, commandName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateExecutableCandidates(string searchPath, string commandName)
    {
        yield return Path.Combine(searchPath, commandName);

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(searchPath, string.Concat(commandName, extension));
        }
    }

    private async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetProviderStatusesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        return forceRefresh
            ? await providerStatusReader.RefreshAsync(cancellationToken)
            : await providerStatusReader.ReadAsync(cancellationToken);
    }

    private void InvalidateProviderStatusSnapshot()
    {
        providerStatusReader.Invalidate();
    }

    private async Task NormalizeLegacyAgentProfilesAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var legacyDebugModel = AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug);
        var legacyAgents = await dbContext.AgentProfiles
            .Where(record =>
                (record.ProviderKind != (int)AgentProviderKind.Debug &&
                 record.ModelName == legacyDebugModel) ||
                string.IsNullOrWhiteSpace(record.Description))
            .ToListAsync(cancellationToken);
        if (legacyAgents.Count == 0)
        {
            return;
        }

        foreach (var agent in legacyAgents)
        {
            var providerKind = (AgentProviderKind)agent.ProviderKind;
            if (agent.ProviderKind != (int)AgentProviderKind.Debug &&
                agent.ModelName == legacyDebugModel)
            {
                agent.ModelName = AgentSessionDefaults.GetDefaultModel(providerKind);
                AgentSessionServiceLog.LegacyAgentProfileNormalized(logger, agent.Id, providerKind, agent.ModelName);
            }

            if (string.IsNullOrWhiteSpace(agent.Description))
            {
                agent.Description = ResolveAgentDescription(string.Empty, agent.SystemPrompt);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private async ValueTask<Result<SessionStreamEntry>> TryAppendSendFailureEntryAsync(
        LocalAgentSessionDbContext dbContext,
        SessionRecord session,
        SessionId sessionId,
        Guid agentId,
        string providerDisplayName,
        Problem problem,
        CancellationToken cancellationToken)
    {
        try
        {
            var failureEntry = CreateEntryRecord(
                sessionId,
                SessionStreamEntryKind.Error,
                StatusAuthor,
                CreateSendFailureText(providerDisplayName, problem.ToDisplayMessage("Message send failed.")),
                timeProvider.GetUtcNow(),
                accentLabel: ErrorAccentLabel);
            dbContext.SessionEntries.Add(failureEntry);
            session.UpdatedAt = failureEntry.Timestamp;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result<SessionStreamEntry>.Succeed(MapEntry(failureEntry));
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SendFailed(logger, exception, sessionId, agentId);
            return Result<SessionStreamEntry>.Fail(exception);
        }
    }

    private static string CreateSendFailureText(string providerDisplayName, string message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Message send failed."
            : message.Trim();
        normalizedMessage = normalizedMessage.TrimEnd('.');
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{providerDisplayName} failed before responding: {normalizedMessage}.");
    }

    private static string CreateToolStartText(AgentProviderKind providerKind)
    {
        return providerKind == AgentProviderKind.Debug
            ? "Preparing local debug workflow."
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Launching {providerKind.GetDisplayName()} in the local playground.");
    }

    private static string CreateToolDoneText(AgentProviderKind providerKind)
    {
        return providerKind == AgentProviderKind.Debug
            ? "Debug workflow finished."
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{providerKind.GetDisplayName()} turn finished.");
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
        var providerKind = (AgentProviderKind)agent.ProviderKind;
        var providerDisplayName = providerKind.GetDisplayName();

        return new SessionListItem(
            new SessionId(record.Id),
            record.Title,
            preview,
            providerDisplayName,
            record.UpdatedAt,
            new AgentProfileId(agent.Id),
            agent.Name,
            providerDisplayName);
    }

    private static AgentProfileSummary MapAgentSummary(AgentProfileRecord record)
    {
        var providerKind = (AgentProviderKind)record.ProviderKind;
        return new AgentProfileSummary(
            new AgentProfileId(record.Id),
            record.Name,
            string.IsNullOrWhiteSpace(record.Description)
                ? ResolveAgentDescription(string.Empty, record.SystemPrompt)
                : record.Description,
            providerKind,
            providerKind.GetDisplayName(),
            record.ModelName,
            record.SystemPrompt,
            record.CreatedAt);
    }

    private static List<AgentProfileRecord> OrderAgents(IReadOnlyList<AgentProfileRecord> agents)
    {
        var hasNonSystemAgents = agents.Any(record => !AgentSessionDefaults.IsSystemAgent(record.Name));

        return agents
            .OrderBy(record => hasNonSystemAgents && AgentSessionDefaults.IsSystemAgent(record.Name) ? 1 : 0)
            .ThenByDescending(record => record.CreatedAt)
            .ThenBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Dispose()
    {
        _initializationGate.Dispose();
    }

    private static string ResolveAgentDescription(string description, string systemPrompt)
    {
        var normalizedDescription = description.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedDescription))
        {
            return normalizedDescription;
        }

        return string.IsNullOrWhiteSpace(systemPrompt)
            ? string.Empty
            : AgentSessionDefaults.CreateAgentDescription(systemPrompt);
    }
}
