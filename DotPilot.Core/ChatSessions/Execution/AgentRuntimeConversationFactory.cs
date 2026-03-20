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
        var historyProvider = new FolderChatHistoryProvider(
            serviceProvider.GetRequiredService<LocalAgentChatHistoryStore>());
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
                CreateChatClient(descriptor.ProviderKind, agentRecord.Name, sessionId, agentRecord.ModelName)),
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1859:Use concrete types when possible for improved performance",
        Justification = "The runtime conversation factory intentionally preserves the IChatClient abstraction across provider-backed chat clients.")]
    private IChatClient CreateChatClient(
        AgentProviderKind providerKind,
        string agentName,
        SessionId sessionId,
        string modelName)
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
            var modelPath = ResolveLocalModelPath(providerKind);
            return new OnnxRuntimeGenAIChatClient(modelPath);
        }

        if (providerKind == AgentProviderKind.LlamaSharp)
        {
            var modelPath = ResolveLocalModelPath(providerKind);
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

    private string ResolvePlaygroundDirectory(SessionId sessionId)
    {
        var directory = AgentSessionStoragePaths.ResolvePlaygroundDirectory(storageOptions, sessionId);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static bool ShouldUseFolderChatHistory(AgentProviderKind providerKind)
    {
        return providerKind is AgentProviderKind.Debug or AgentProviderKind.Onnx or AgentProviderKind.LlamaSharp;
    }

    private static string ResolveLocalModelPath(AgentProviderKind providerKind)
    {
        var configuration = LocalModelProviderConfigurationReader.Read(providerKind);
        if (configuration.IsReady && !string.IsNullOrWhiteSpace(configuration.ModelPath))
        {
            return configuration.ModelPath;
        }

        throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is not configured. Set {1} before starting a local session.",
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
}
