using DotPilot.Core.AgentBuilder;
using DotPilot.Core.ControlPlaneDomain;
using DotPilot.Core.Providers;
using ManagedCode.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.ChatSessions;

internal sealed class AgentSessionService(
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    AgentExecutionLoggingMiddleware executionLoggingMiddleware,
    IAgentProviderStatusReader providerStatusReader,
    AgentRuntimeConversationFactory runtimeConversationFactory,
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
    private const string ToolAccentLabel = "tool";
    private const string StatusAccentLabel = "status";
    private const string ErrorAccentLabel = "error";
    internal static readonly System.Text.CompositeFormat LiveExecutionUnavailableCompositeFormat =
        System.Text.CompositeFormat.Parse(NotYetImplementedFormat);
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
                ProviderKind = (int)command.ProviderKind,
                ModelName = modelName,
                SystemPrompt = systemPrompt,
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

    public async ValueTask<Result<SessionTranscriptSnapshot>> CreateSessionAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            await EnsureInitializedAsync(cancellationToken);
            var sessionTitle = command.Title.Trim();
            AgentSessionServiceLog.SessionCreationStarted(logger, sessionTitle, command.AgentProfileId);

            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var agent = await dbContext.AgentProfiles
                .FirstOrDefaultAsync(record => record.Id == command.AgentProfileId.Value, cancellationToken);
            if (agent is null)
            {
                return Result<SessionTranscriptSnapshot>.FailNotFound($"Agent '{command.AgentProfileId}' was not found.");
            }

            var providerProfile = AgentSessionProviderCatalog.Get((AgentProviderKind)agent.ProviderKind);
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
            if (providerProfile.SupportsLiveExecution)
            {
                await runtimeConversationFactory.InitializeAsync(agent, sessionId, cancellationToken);
            }

            AgentSessionServiceLog.SessionCreated(logger, sessionId, session.Title, command.AgentProfileId);

            var reloaded = await GetSessionAsync(sessionId, cancellationToken);
            return reloaded.IsSuccess
                ? reloaded
                : Result<SessionTranscriptSnapshot>.Fail("SessionCreationFailed", "Created session could not be reloaded.");
        }
        catch (Exception exception)
        {
            AgentSessionServiceLog.SessionCreationFailed(logger, exception, command.Title, command.AgentProfileId);
            return Result<SessionTranscriptSnapshot>.Fail(exception);
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
            Result<(SessionRecord Session, AgentProfileRecord Agent, AgentSessionProviderProfile ProviderProfile, DateTimeOffset Timestamp)> contextResult;
            try
            {
                var sessionRecord = await dbContext.Sessions
                    .FirstOrDefaultAsync(record => record.Id == command.SessionId.Value, cancellationToken);
                if (sessionRecord is null)
                {
                    contextResult = Result<(SessionRecord, AgentProfileRecord, AgentSessionProviderProfile, DateTimeOffset)>.FailNotFound(
                        $"Session '{command.SessionId}' was not found.");
                }
                else
                {
                    var agentRecord = await dbContext.AgentProfiles
                        .FirstOrDefaultAsync(record => record.Id == sessionRecord.PrimaryAgentProfileId, cancellationToken);
                    if (agentRecord is null)
                    {
                        contextResult = Result<(SessionRecord, AgentProfileRecord, AgentSessionProviderProfile, DateTimeOffset)>.FailNotFound(
                            $"Agent '{sessionRecord.PrimaryAgentProfileId}' was not found.");
                    }
                    else
                    {
                        contextResult = Result<(SessionRecord, AgentProfileRecord, AgentSessionProviderProfile, DateTimeOffset)>.Succeed((
                            sessionRecord,
                            agentRecord,
                            AgentSessionProviderCatalog.Get((AgentProviderKind)agentRecord.ProviderKind),
                            timeProvider.GetUtcNow()));
                    }
                }
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, Guid.Empty);
                contextResult = Result<(SessionRecord, AgentProfileRecord, AgentSessionProviderProfile, DateTimeOffset)>.Fail(exception);
            }

            if (contextResult.IsFailed)
            {
                yield return Result<SessionStreamEntry>.Fail(contextResult.Problem!);
                yield break;
            }

            var (session, agent, providerProfile, now) = contextResult.Value;
            AgentSessionServiceLog.SendStarted(
                logger,
                command.SessionId,
                agent.Id,
                providerProfile.Kind);

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
                $"Running {agent.Name} with {providerProfile.DisplayName}.",
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
                    providerStatuses.First(status => status.Kind == providerProfile.Kind));
            }
            catch (Exception exception)
            {
                AgentSessionServiceLog.SendFailed(logger, exception, command.SessionId, agent.Id);
                providerStatusResult = Result<ProviderStatusDescriptor>.Fail(exception);
            }

            if (providerStatusResult.IsFailed)
            {
                yield return Result<SessionStreamEntry>.Fail(providerStatusResult.Problem!);
                yield break;
            }

            var providerStatus = providerStatusResult.Value;
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

            if (!providerProfile.SupportsLiveExecution)
            {
                AgentSessionServiceLog.SendBlockedNotWired(logger, command.SessionId, providerProfile.Kind);
                var notImplementedEntry = CreateEntryRecord(
                    command.SessionId,
                    SessionStreamEntryKind.Error,
                    StatusAuthor,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        LiveExecutionUnavailableCompositeFormat,
                        providerProfile.DisplayName),
                    timeProvider.GetUtcNow(),
                    accentLabel: ErrorAccentLabel);
                dbContext.SessionEntries.Add(notImplementedEntry);
                session.UpdatedAt = notImplementedEntry.Timestamp;
                await dbContext.SaveChangesAsync(cancellationToken);

                yield return Result<SessionStreamEntry>.Succeed(MapEntry(notImplementedEntry));
                yield break;
            }

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
                yield return Result<SessionStreamEntry>.Fail(runtimeConversationResult.Problem!);
                yield break;
            }

            var runtimeConversation = runtimeConversationResult.Value;
            var toolStartEntry = CreateEntryRecord(
                command.SessionId,
                SessionStreamEntryKind.ToolStarted,
                ToolAuthor,
                CreateToolStartText(providerProfile),
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
                providerProfile.Kind,
                agent.ModelName);

            await using var updateEnumerator = runtimeConversation.Agent.RunStreamingAsync(
                    command.Message.Trim(),
                    runtimeConversation.Session,
                    runConfiguration.Options,
                    cancellationToken)
                .GetAsyncEnumerator(cancellationToken);

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
                    CreateToolDoneText(providerProfile),
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

            yield return completionResult;
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
            var debugPreference = await dbContext.ProviderPreferences
                .FirstOrDefaultAsync(
                    record => record.ProviderKind == (int)AgentProviderKind.Debug,
                    cancellationToken);

            if (debugPreference is null)
            {
                debugPreference = new ProviderPreferenceRecord
                {
                    ProviderKind = (int)AgentProviderKind.Debug,
                };
                dbContext.ProviderPreferences.Add(debugPreference);
            }

            debugPreference.IsEnabled = true;
            debugPreference.UpdatedAt = timeProvider.GetUtcNow();
            AgentSessionServiceLog.DefaultProviderEnabled(logger, AgentProviderKind.Debug);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await NormalizeLegacyAgentProfilesAsync(dbContext, cancellationToken);

        var hasAgents = await dbContext.AgentProfiles.AnyAsync(cancellationToken);
        if (hasAgents)
        {
            return;
        }

        var providerSnapshot = await GetProviderStatusesAsync(forceRefresh: true, cancellationToken);
        var providerKind = providerSnapshot
            .Where(provider =>
                provider.CanCreateAgents &&
                AgentSessionProviderCatalog.Get(provider.Kind).SupportsLiveExecution)
            .Select(static provider => provider.Kind)
            .FirstOrDefault(AgentProviderKind.Debug);
        var record = new AgentProfileRecord
        {
            Id = Guid.CreateVersion7(),
            Name = AgentSessionDefaults.SystemAgentName,
            ProviderKind = (int)providerKind,
            ModelName = AgentSessionDefaults.GetDefaultModel(providerKind),
            SystemPrompt = AgentSessionDefaults.SystemAgentPrompt,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        dbContext.AgentProfiles.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        AgentSessionServiceLog.DefaultAgentSeeded(logger, record.Id, providerKind, record.ModelName);
    }

    private async ValueTask<IReadOnlyList<ProviderStatusDescriptor>> GetProviderStatusesAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        _ = forceRefresh;
        return await providerStatusReader.ReadAsync(cancellationToken);
    }

    private static void InvalidateProviderStatusSnapshot()
    {
    }

    private async Task NormalizeLegacyAgentProfilesAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var legacyDebugModel = AgentSessionDefaults.GetDefaultModel(AgentProviderKind.Debug);
        var legacyAgents = await dbContext.AgentProfiles
            .Where(record =>
                record.ProviderKind != (int)AgentProviderKind.Debug &&
                record.ModelName == legacyDebugModel)
            .ToListAsync(cancellationToken);
        if (legacyAgents.Count == 0)
        {
            return;
        }

        foreach (var agent in legacyAgents)
        {
            var providerKind = (AgentProviderKind)agent.ProviderKind;
            agent.ModelName = AgentSessionDefaults.GetDefaultModel(providerKind);
            AgentSessionServiceLog.LegacyAgentProfileNormalized(logger, agent.Id, providerKind, agent.ModelName);
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

    private static string CreateToolStartText(AgentSessionProviderProfile providerProfile)
    {
        return providerProfile.Kind == AgentProviderKind.Debug
            ? "Preparing local debug workflow."
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Launching {providerProfile.DisplayName} in the local playground.");
    }

    private static string CreateToolDoneText(AgentSessionProviderProfile providerProfile)
    {
        return providerProfile.Kind == AgentProviderKind.Debug
            ? "Debug workflow finished."
            : string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{providerProfile.DisplayName} turn finished.");
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
            (AgentProviderKind)record.ProviderKind,
            providerProfile.DisplayName,
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
}
