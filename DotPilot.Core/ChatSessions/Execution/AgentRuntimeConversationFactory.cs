using System.Globalization;
using DotPilot.Core.Providers;
using GitHub.Copilot.SDK;
using ManagedCode.ClaudeCodeSharpSDK.Configuration;
using ManagedCode.ClaudeCodeSharpSDK.Extensions.AI;
using ManagedCode.CodexSharpSDK.Client;
using ManagedCode.CodexSharpSDK.Configuration;
using ManagedCode.CodexSharpSDK.Extensions.AI;
using ManagedCode.GeminiSharpSDK.Configuration;
using ManagedCode.GeminiSharpSDK.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntimeGenAI;
using ClaudeThreadOptions = ManagedCode.ClaudeCodeSharpSDK.Client.ThreadOptions;
using CodexThreadOptions = ManagedCode.CodexSharpSDK.Client.ThreadOptions;
using GeminiApprovalMode = ManagedCode.GeminiSharpSDK.Client.ApprovalMode;
using GeminiSandboxMode = ManagedCode.GeminiSharpSDK.Client.SandboxMode;
using GeminiThreadOptions = ManagedCode.GeminiSharpSDK.Client.ThreadOptions;

namespace DotPilot.Core.ChatSessions;

internal sealed class AgentRuntimeConversationFactory(
    AgentSessionStorageOptions storageOptions,
    AgentExecutionLoggingMiddleware executionLoggingMiddleware,
    LocalAgentSessionStateStore sessionStateStore,
    LocalAgentChatHistoryStore chatHistoryStore,
    IDbContextFactory<LocalAgentSessionDbContext> dbContextFactory,
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<AgentRuntimeConversationFactory> logger)
{
    public async ValueTask InitializeAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        AgentRuntimeConversationFactoryLog.InitializeStarted(logger, sessionId, agentRecord.Id);
        if (ShouldUseTransientRuntimeConversation(agentRecord))
        {
            AgentRuntimeConversationFactoryLog.TransientRuntimeConversation(logger, sessionId, agentRecord.Id);
            return;
        }

        var runtimeSession = await LoadOrCreateAsync(agentRecord, sessionId, cancellationToken);
        await sessionStateStore.SaveAsync(runtimeSession.Agent, runtimeSession.Session, sessionId, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var agentRuntimeId = agentRecord.Id.ToString("N", CultureInfo.InvariantCulture);
            AgentRuntimeConversationFactoryLog.SessionSaved(
                logger,
                sessionId,
                agentRuntimeId);
        }
    }

    public async ValueTask<RuntimeConversationContext> LoadOrCreateAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var useTransientConversation = ShouldUseTransientRuntimeConversation(agentRecord);
        var historyProvider = new FolderChatHistoryProvider(chatHistoryStore);
        var descriptor = CreateExecutionDescriptor(agentRecord);
        var agent = await CreateAgentAsync(agentRecord, descriptor, historyProvider, sessionId, cancellationToken);
        if (useTransientConversation)
        {
            var transientSession = await CreateNewSessionAsync(agent, sessionId, cancellationToken);
            AgentRuntimeConversationFactoryLog.TransientRuntimeConversation(logger, sessionId, agentRecord.Id);
            return new RuntimeConversationContext(agent, transientSession, descriptor, IsTransient: true);
        }

        var session = await sessionStateStore.TryLoadAsync(agent, sessionId, cancellationToken);
        if (session is null)
        {
            session = await CreateNewSessionAsync(agent, sessionId, cancellationToken);
            await sessionStateStore.SaveAsync(agent, session, sessionId, cancellationToken);
            AgentRuntimeConversationFactoryLog.SessionCreated(logger, sessionId, agentRecord.Id);
        }
        else
        {
            AgentRuntimeConversationFactoryLog.SessionLoaded(logger, sessionId, agentRecord.Id);
        }

        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return new RuntimeConversationContext(agent, session, descriptor);
    }

    public ValueTask SaveAsync(
        RuntimeConversationContext runtimeContext,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtimeContext);
        if (runtimeContext.IsTransient)
        {
            return ValueTask.CompletedTask;
        }

        AgentRuntimeConversationFactoryLog.SessionSaved(logger, sessionId, runtimeContext.Agent.Id);
        return sessionStateStore.SaveAsync(runtimeContext.Agent, runtimeContext.Session, sessionId, cancellationToken);
    }

    public async ValueTask CloseAsync(
        AgentProfileRecord? agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var agentId = agentRecord?.Id ?? Guid.Empty;
        AgentRuntimeConversationFactoryLog.CloseStarted(logger, sessionId, agentId);

        if (agentRecord is not null && ShouldAttemptProviderSessionTeardown(agentRecord))
        {
            await TryCloseProviderSessionAsync(agentRecord, sessionId, cancellationToken);
        }

        await DeleteLocalArtifactsAsync(sessionId, cancellationToken);
        AgentRuntimeConversationFactoryLog.SessionClosed(logger, sessionId, agentId);
    }

    private bool ShouldUseTransientRuntimeConversation(AgentProfileRecord agentRecord)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        var providerKind = (AgentProviderKind)agentRecord.ProviderKind;
        return storageOptions.PreferTransientRuntimeConversation ||
            (OperatingSystem.IsBrowser() && providerKind == AgentProviderKind.Debug);
    }

    private static async ValueTask<AgentSession> CreateNewSessionAsync(
        AIAgent agent,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var session = await agent.CreateSessionAsync(cancellationToken);
        FolderChatHistoryProvider.BindToSession(session, sessionId);
        return session;
    }

    private async ValueTask<AIAgent> CreateAgentAsync(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        FolderChatHistoryProvider historyProvider,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        AgentRuntimeConversationFactoryLog.AgentRuntimeCreated(
            logger,
            agentRecord.Id,
            agentRecord.Name,
            descriptor.ProviderKind);

        var agent = descriptor.ProviderKind switch
        {
            AgentProviderKind.GitHubCopilot => await CreateGitHubCopilotAgentAsync(
                agentRecord,
                descriptor,
                sessionId,
                cancellationToken),
            _ => CreateChatClientAgent(
                agentRecord,
                descriptor,
                ShouldUseFolderChatHistory(descriptor.ProviderKind) ? historyProvider : null,
                await CreateChatClientAsync(
                    descriptor.ProviderKind,
                    agentRecord.Name,
                    sessionId,
                    agentRecord.ModelName,
                    cancellationToken)),
        };

        return executionLoggingMiddleware.AttachAgentRunLogging(agent, descriptor);
    }

    private static AgentExecutionDescriptor CreateExecutionDescriptor(AgentProfileRecord agentRecord)
    {
        var providerKind = (AgentProviderKind)agentRecord.ProviderKind;
        return new AgentExecutionDescriptor(
            agentRecord.Id,
            agentRecord.Name,
            providerKind,
            providerKind.GetDisplayName(),
            agentRecord.ModelName);
    }

    private static bool ShouldAttemptProviderSessionTeardown(AgentProfileRecord agentRecord)
    {
        ArgumentNullException.ThrowIfNull(agentRecord);

        return (AgentProviderKind)agentRecord.ProviderKind == AgentProviderKind.GitHubCopilot;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The runtime conversation factory intentionally preserves the IChatClient abstraction across provider-backed chat clients.")]
    private async ValueTask<IChatClient> CreateChatClientAsync(
        AgentProviderKind providerKind,
        string agentName,
        SessionId sessionId,
        string modelName,
        CancellationToken cancellationToken)
    {
        if (providerKind == AgentProviderKind.Debug)
        {
            return new DebugChatClient(agentName, timeProvider);
        }

        if (providerKind == AgentProviderKind.Codex)
        {
            var codexExecutablePath = ResolveExecutablePath(providerKind);
            return new CodexChatClient(new CodexChatClientOptions
            {
                CodexOptions = new CodexOptions
                {
                    CodexExecutablePath = codexExecutablePath,
                },
                DefaultModel = modelName,
                DefaultThreadOptions = new CodexThreadOptions
                {
                    Model = modelName,
                    ModelReasoningEffort = ModelReasoningEffort.High,
                    SkipGitRepoCheck = true,
                    WorkingDirectory = ResolvePlaygroundDirectory(sessionId),
                },
            });
        }

        if (providerKind == AgentProviderKind.ClaudeCode)
        {
            var claudeExecutablePath = ResolveExecutablePath(providerKind);
            return new ClaudeChatClient(new ClaudeChatClientOptions
            {
                ClaudeOptions = new ClaudeOptions
                {
                    ClaudeExecutablePath = claudeExecutablePath,
                },
                DefaultModel = modelName,
                DefaultThreadOptions = new ClaudeThreadOptions
                {
                    Model = modelName,
                    WorkingDirectory = ResolvePlaygroundDirectory(sessionId),
                },
            });
        }

        if (providerKind == AgentProviderKind.Gemini)
        {
            var geminiExecutablePath = ResolveExecutablePath(providerKind);
            return new GeminiChatClient(new GeminiChatClientOptions
            {
                GeminiOptions = new GeminiOptions
                {
                    GeminiExecutablePath = geminiExecutablePath,
                },
                DefaultModel = modelName,
                DefaultThreadOptions = new GeminiThreadOptions
                {
                    Model = modelName,
                    WorkingDirectory = ResolvePlaygroundDirectory(sessionId),
                    SandboxMode = GeminiSandboxMode.WorkspaceWrite,
                    ApprovalPolicy = GeminiApprovalMode.Yolo,
                },
            });
        }

        if (providerKind == AgentProviderKind.Onnx)
        {
            var modelPath = await ResolveLocalModelPathAsync(providerKind, modelName, cancellationToken);
            return new OnnxRuntimeGenAIChatClient(modelPath);
        }

        if (providerKind == AgentProviderKind.LlamaSharp)
        {
            var modelPath = await ResolveLocalModelPathAsync(providerKind, modelName, cancellationToken);
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            return new LlamaLocalChatClient(
                modelPath,
                loggerFactory?.CreateLogger<LlamaLocalChatClient>());
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} live execution is unavailable.",
                providerKind.GetDisplayName()));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The factory returns the concrete ChatClientAgent only for the chat-client-backed providers and keeps the outer flow on AIAgent.")]
    private ChatClientAgent CreateChatClientAgent(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        FolderChatHistoryProvider? historyProvider,
        IChatClient chatClient)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        var options = new ChatClientAgentOptions
        {
            Id = agentRecord.Id.ToString("N", CultureInfo.InvariantCulture),
            Name = agentRecord.Name,
            Description = descriptor.ProviderDisplayName,
            UseProvidedChatClientAsIs = true,
            ChatOptions = new ChatOptions
            {
                Instructions = agentRecord.SystemPrompt,
                ModelId = agentRecord.ModelName,
            },
        };
        if (historyProvider is not null)
        {
            options.ChatHistoryProvider = historyProvider;
        }

        return (ChatClientAgent)chatClient.AsAIAgent(options, loggerFactory, serviceProvider);
    }

    private async ValueTask<AIAgent> CreateGitHubCopilotAgentAsync(
        AgentProfileRecord agentRecord,
        AgentExecutionDescriptor descriptor,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var workingDirectory = ResolvePlaygroundDirectory(sessionId);
        var copilotExecutablePath = ResolveExecutablePath(AgentProviderKind.GitHubCopilot) ??
            AgentProviderKind.GitHubCopilot.GetCommandName();
        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            CliPath = copilotExecutablePath,
            AutoStart = false,
            UseStdio = true,
        });

        await copilotClient.StartAsync(cancellationToken);

        return copilotClient.AsAIAgent(
            new SessionConfig
            {
                Model = agentRecord.ModelName,
                OnPermissionRequest = PermissionHandler.ApproveAll,
                SystemMessage = new SystemMessageConfig
                {
                    Content = agentRecord.SystemPrompt,
                },
                WorkingDirectory = workingDirectory,
            },
            ownsClient: true,
            id: agentRecord.Id.ToString("N", CultureInfo.InvariantCulture),
            name: agentRecord.Name,
            description: descriptor.ProviderDisplayName);
    }

    private async ValueTask TryCloseProviderSessionAsync(
        AgentProfileRecord agentRecord,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        AIAgent? agent = null;
        try
        {
            var historyProvider = new FolderChatHistoryProvider(chatHistoryStore);
            var descriptor = CreateExecutionDescriptor(agentRecord);
            agent = await CreateAgentAsync(agentRecord, descriptor, historyProvider, sessionId, cancellationToken);
            var session = await sessionStateStore.TryLoadAsync(agent, sessionId, cancellationToken);
            if (session is not GitHubCopilotAgentSession copilotSession ||
                string.IsNullOrWhiteSpace(copilotSession.SessionId))
            {
                return;
            }

            var copilotClient = agent.GetService<CopilotClient>();
            if (copilotClient is null)
            {
                return;
            }

            await copilotClient.DeleteSessionAsync(copilotSession.SessionId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            AgentRuntimeConversationFactoryLog.ProviderSessionTeardownFailed(
                logger,
                exception,
                sessionId,
                agentRecord.Id);
        }
        finally
        {
            if (agent is not null)
            {
                await DisposeAgentAsync(agent);
            }
        }
    }

    private string ResolvePlaygroundDirectory(SessionId sessionId)
    {
        var directory = AgentSessionStoragePaths.ResolvePlaygroundDirectory(storageOptions, sessionId);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private async ValueTask DeleteLocalArtifactsAsync(
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        await sessionStateStore.DeleteAsync(sessionId, cancellationToken);
        await chatHistoryStore.DeleteAsync(sessionId, cancellationToken);
        await LocalStorageDeletion.DeleteDirectoryIfExistsAsync(
            AgentSessionStoragePaths.ResolvePlaygroundDirectory(storageOptions, sessionId),
            cancellationToken);
    }

    private static bool ShouldUseFolderChatHistory(AgentProviderKind providerKind)
    {
        return providerKind is AgentProviderKind.Debug or AgentProviderKind.Onnx or AgentProviderKind.LlamaSharp;
    }

    private async ValueTask<string> ResolveLocalModelPathAsync(
        AgentProviderKind providerKind,
        string modelName,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var preference = await dbContext.ProviderPreferences
            .FirstOrDefaultAsync(record => record.ProviderKind == (int)providerKind, cancellationToken);
        var localModels = await dbContext.ProviderLocalModels
            .Where(record => record.ProviderKind == (int)providerKind)
            .ToArrayAsync(cancellationToken);
        var configuration = await LocalModelProviderConfigurationReader.ReadAsync(
            providerKind,
            localModels,
            preference?.LocalModelPath,
            cancellationToken).ConfigureAwait(false);
        var resolvedModelPath = configuration.ResolveModelPath(modelName);
        if (configuration.IsReady && !string.IsNullOrWhiteSpace(resolvedModelPath))
        {
            return resolvedModelPath;
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is not configured. Choose a local model in Settings or set {1} before starting a local session.",
                providerKind.GetDisplayName(),
                configuration.PrimaryEnvironmentVariableName));
    }

    private static string? ResolveExecutablePath(AgentProviderKind providerKind)
    {
        if (OperatingSystem.IsBrowser())
        {
            return null;
        }

        var commandName = providerKind.GetCommandName();
        var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var searchPath in searchPaths)
        {
            foreach (var candidate in EnumerateCandidates(searchPath, commandName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates(string searchPath, string commandName)
    {
        yield return Path.Combine(searchPath, commandName);

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var pathext = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var extension in pathext)
        {
            yield return Path.Combine(searchPath, commandName + extension);
        }
    }

    private static async ValueTask DisposeAgentAsync(AIAgent agent)
    {
        switch (agent)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
