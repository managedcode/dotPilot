using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Tests.Features.RuntimeFoundation;

public sealed class AgentFrameworkRuntimeClientTests
{
    private const string ApprovalPrompt = "Execute the runtime flow and stop for approval before any file change.";
    private const string PlanPrompt = "Plan the embedded runtime rollout.";
    private const string ApprovedResumeSummary = "Approved by the operator.";
    private const string RejectedResumeSummary = "Rejected by the operator.";
    private const string ResumeRejectedKind = "approval-rejected";
    private const string ArchiveFileName = "archive.json";
    private const string ReplayFileName = "replay.md";
    private static readonly DateTimeOffset FixedTimestamp = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);

    [Test]
    public async Task ExecuteAsyncPersistsAReplayArchiveForPlanMode()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = host.Services.GetRequiredService<IAgentRuntimeClient>();
        var request = CreateRequest(PlanPrompt, AgentExecutionMode.Plan);

        var result = await client.ExecuteAsync(request, CancellationToken.None);
        var archiveResult = await client.GetSessionArchiveAsync(request.SessionId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.NextPhase.Should().Be(SessionPhase.Plan);
        archiveResult.IsSuccess.Should().BeTrue();
        archiveResult.Value!.Phase.Should().Be(SessionPhase.Plan);
        archiveResult.Value.Replay.Should().ContainSingle(entry => entry.Kind == "run-started");
        File.Exists(Path.Combine(runtimeDirectory.Root, request.SessionId.ToString(), ArchiveFileName)).Should().BeTrue();
        File.Exists(Path.Combine(runtimeDirectory.Root, request.SessionId.ToString(), ReplayFileName)).Should().BeTrue();
    }

    [Test]
    public async Task ExecuteAsyncPausesForApprovalAndResumeAsyncCompletesAfterHostRestart()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var request = CreateRequest(ApprovalPrompt, AgentExecutionMode.Execute);

        {
            using var firstHost = CreateHost(runtimeDirectory.Root);

            await firstHost.StartAsync();
            var firstClient = firstHost.Services.GetRequiredService<IAgentRuntimeClient>();

            var pausedResult = await firstClient.ExecuteAsync(request, CancellationToken.None);

            pausedResult.IsSuccess.Should().BeTrue();
            pausedResult.Value!.NextPhase.Should().Be(SessionPhase.Paused);
            pausedResult.Value.ApprovalState.Should().Be(ApprovalState.Pending);
        }

        {
            using var secondHost = CreateHost(runtimeDirectory.Root);

            await secondHost.StartAsync();
            var secondClient = secondHost.Services.GetRequiredService<IAgentRuntimeClient>();

            var archiveBeforeResume = await secondClient.GetSessionArchiveAsync(request.SessionId, CancellationToken.None);
            var resumedResult = await secondClient.ResumeAsync(
                new AgentTurnResumeRequest(request.SessionId, ApprovalState.Approved, ApprovedResumeSummary),
                CancellationToken.None);
            var archiveAfterResume = await secondClient.GetSessionArchiveAsync(request.SessionId, CancellationToken.None);
            var grainFactory = secondHost.Services.GetRequiredService<IGrainFactory>();

            archiveBeforeResume.IsSuccess.Should().BeTrue();
            archiveBeforeResume.Value!.CheckpointId.Should().NotBeNullOrWhiteSpace();
            resumedResult.IsSuccess.Should().BeTrue();
            resumedResult.Value!.NextPhase.Should().Be(SessionPhase.Execute);
            resumedResult.Value.ApprovalState.Should().Be(ApprovalState.Approved);
            archiveAfterResume.IsSuccess.Should().BeTrue();
            archiveAfterResume.Value!.Replay.Select(entry => entry.Kind).Should().Contain(["approval-pending", "run-resumed", "run-completed"]);
            (await grainFactory.GetGrain<ISessionGrain>(request.SessionId.ToString()).GetAsync())!.Phase.Should().Be(SessionPhase.Execute);
        }
    }

    [Test]
    public async Task ResumeAsyncPersistsRejectedApprovalAsFailedReplay()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = host.Services.GetRequiredService<IAgentRuntimeClient>();
        var request = CreateRequest(ApprovalPrompt, AgentExecutionMode.Execute);

        _ = await client.ExecuteAsync(request, CancellationToken.None);
        var rejectedResult = await client.ResumeAsync(
            new AgentTurnResumeRequest(request.SessionId, ApprovalState.Rejected, RejectedResumeSummary),
            CancellationToken.None);
        var archiveResult = await client.GetSessionArchiveAsync(request.SessionId, CancellationToken.None);

        rejectedResult.IsSuccess.Should().BeTrue();
        rejectedResult.Value!.NextPhase.Should().Be(SessionPhase.Failed);
        rejectedResult.Value.ApprovalState.Should().Be(ApprovalState.Rejected);
        archiveResult.IsSuccess.Should().BeTrue();
        archiveResult.Value!.Phase.Should().Be(SessionPhase.Failed);
        archiveResult.Value.Replay.Should().Contain(entry => entry.Kind == ResumeRejectedKind && entry.Phase == SessionPhase.Failed);
        archiveResult.Value.Replay.Should().Contain(entry => entry.Kind == "run-completed" && entry.Phase == SessionPhase.Failed);
    }

    [Test]
    public async Task ResumeAsyncRejectsArchivedSessionsThatAreNoLongerPausedForApproval()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var request = CreateRequest(ApprovalPrompt, AgentExecutionMode.Execute);

        {
            using var firstHost = CreateHost(runtimeDirectory.Root);
            await firstHost.StartAsync();
            var firstClient = firstHost.Services.GetRequiredService<IAgentRuntimeClient>();
            _ = await firstClient.ExecuteAsync(request, CancellationToken.None);
            _ = await firstClient.ResumeAsync(
                new AgentTurnResumeRequest(request.SessionId, ApprovalState.Approved, ApprovedResumeSummary),
                CancellationToken.None);
        }

        {
            using var secondHost = CreateHost(runtimeDirectory.Root);
            await secondHost.StartAsync();
            var secondClient = secondHost.Services.GetRequiredService<IAgentRuntimeClient>();

            var result = await secondClient.ResumeAsync(
                new AgentTurnResumeRequest(request.SessionId, ApprovalState.Approved, ApprovedResumeSummary),
                CancellationToken.None);

            result.IsFailed.Should().BeTrue();
            result.Problem!.HasErrorCode(RuntimeCommunicationProblemCode.ResumeCheckpointMissing).Should().BeTrue();
            result.Problem.Detail.Should().Contain("cannot be resumed");
        }
    }

    [Test]
    public async Task GetSessionArchiveAsyncReturnsMissingProblemWhenNothingWasPersisted()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = host.Services.GetRequiredService<IAgentRuntimeClient>();
        var missingSessionId = SessionId.New();

        var result = await client.GetSessionArchiveAsync(missingSessionId, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Problem!.HasErrorCode(RuntimeCommunicationProblemCode.SessionArchiveMissing).Should().BeTrue();
    }

    [Test]
    public async Task GetSessionArchiveAsyncReturnsCorruptionProblemForInvalidArchivePayload()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var sessionId = SessionId.New();
        var sessionDirectory = Path.Combine(runtimeDirectory.Root, sessionId.ToString());
        Directory.CreateDirectory(sessionDirectory);
        await File.WriteAllTextAsync(Path.Combine(sessionDirectory, ArchiveFileName), "{ invalid json", CancellationToken.None);

        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = host.Services.GetRequiredService<IAgentRuntimeClient>();

        var result = await client.GetSessionArchiveAsync(sessionId, CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Problem!.HasErrorCode(RuntimeCommunicationProblemCode.SessionArchiveCorrupted).Should().BeTrue();
    }

    [Test]
    public async Task ResumeAsyncReturnsCorruptionProblemForInvalidArchivePayload()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var sessionId = SessionId.New();
        var sessionDirectory = Path.Combine(runtimeDirectory.Root, sessionId.ToString());
        Directory.CreateDirectory(sessionDirectory);
        await File.WriteAllTextAsync(Path.Combine(sessionDirectory, ArchiveFileName), "{ invalid json", CancellationToken.None);

        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = host.Services.GetRequiredService<IAgentRuntimeClient>();

        var result = await client.ResumeAsync(
            new AgentTurnResumeRequest(sessionId, ApprovalState.Approved, ApprovedResumeSummary),
            CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Problem!.HasErrorCode(RuntimeCommunicationProblemCode.SessionArchiveCorrupted).Should().BeTrue();
    }

    [Test]
    public async Task AgentFrameworkRuntimeClientUsesTheInjectedTimeProviderForReplayArchiveAndSessionTimestamps()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        using var host = CreateHost(runtimeDirectory.Root);
        await host.StartAsync();
        var client = CreateClient(host.Services, runtimeDirectory.Root, new FixedTimeProvider(FixedTimestamp));
        var request = CreateRequest(PlanPrompt, AgentExecutionMode.Plan);

        var result = await client.ExecuteAsync(request, CancellationToken.None);
        var archiveResult = await client.GetSessionArchiveAsync(request.SessionId, CancellationToken.None);
        var session = await host.Services
            .GetRequiredService<IGrainFactory>()
            .GetGrain<ISessionGrain>(request.SessionId.ToString())
            .GetAsync();

        result.IsSuccess.Should().BeTrue();
        archiveResult.IsSuccess.Should().BeTrue();
        archiveResult.Value!.UpdatedAt.Should().Be(FixedTimestamp);
        archiveResult.Value.Replay.Should().OnlyContain(entry => entry.RecordedAt == FixedTimestamp);
        session.Should().NotBeNull();
        session!.CreatedAt.Should().Be(FixedTimestamp);
        session.UpdatedAt.Should().Be(FixedTimestamp);
    }

    [Test]
    public async Task ExtractCheckpointReturnsNullWhenRunHasNoCheckpointData()
    {
        var workflow = CreateNoCheckpointWorkflow();

        await using var run = await Microsoft.Agents.AI.Workflows.InProcessExecution.RunAsync(
            workflow,
            "no-checkpoint-input",
            SessionId.New().ToString(),
            CancellationToken.None);

        var checkpoint = InvokePrivateStatic<Microsoft.Agents.AI.Workflows.CheckpointInfo?>("ExtractCheckpoint", run);

        checkpoint.Should().BeNull();
    }

    [Test]
    public void TryCreateCheckpointInfoReturnsNullWhenTheCheckpointFilePrefixDoesNotMatchTheWorkflowSession()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var file = CreateCheckpointFile(runtimeDirectory.Root, "different-session_checkpoint-001.json");

        var checkpoint = InvokePrivateStatic<Microsoft.Agents.AI.Workflows.CheckpointInfo?>("TryCreateCheckpointInfo", "expected-session", file);

        checkpoint.Should().BeNull();
    }

    [Test]
    public void TryCreateCheckpointInfoReturnsNullWhenTheCheckpointFileHasNoIdentifierSuffix()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var file = CreateCheckpointFile(runtimeDirectory.Root, "expected-session_.json");

        var checkpoint = InvokePrivateStatic<Microsoft.Agents.AI.Workflows.CheckpointInfo?>("TryCreateCheckpointInfo", "expected-session", file);

        checkpoint.Should().BeNull();
    }

    [Test]
    public void TryCreateCheckpointInfoReturnsCheckpointMetadataForMatchingCheckpointFileNames()
    {
        using var runtimeDirectory = new TemporaryRuntimePersistenceDirectory();
        var file = CreateCheckpointFile(runtimeDirectory.Root, "expected-session_checkpoint-001.json");

        var checkpoint = InvokePrivateStatic<Microsoft.Agents.AI.Workflows.CheckpointInfo?>("TryCreateCheckpointInfo", "expected-session", file);

        checkpoint.Should().NotBeNull();
        checkpoint!.SessionId.Should().Be("expected-session");
        checkpoint.CheckpointId.Should().Be("checkpoint-001");
    }

    private static Microsoft.Extensions.Hosting.IHost CreateHost(string rootDirectory)
    {
        var options = CreateHostOptions();
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDotPilotEmbeddedRuntime(options)
            .ConfigureServices((_, services) => services.AddDesktopRuntimeFoundation(new RuntimePersistenceOptions
            {
                RootDirectoryPath = rootDirectory,
            }))
            .Build();
    }

    private static EmbeddedRuntimeHostOptions CreateHostOptions()
    {
        return new EmbeddedRuntimeHostOptions
        {
            ClusterId = $"dotpilot-runtime-{Guid.NewGuid():N}",
            ServiceId = $"dotpilot-runtime-service-{Guid.NewGuid():N}",
            SiloPort = GetFreeTcpPort(),
            GatewayPort = GetFreeTcpPort(),
        };
    }

    private static AgentTurnRequest CreateRequest(string prompt, AgentExecutionMode mode)
    {
        return new AgentTurnRequest(SessionId.New(), AgentProfileId.New(), prompt, mode, ProviderConnectionStatus.Available);
    }

    private static AgentFrameworkRuntimeClient CreateClient(IServiceProvider services, string rootDirectory, TimeProvider timeProvider)
    {
        return (AgentFrameworkRuntimeClient)Activator.CreateInstance(
            typeof(AgentFrameworkRuntimeClient),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                services.GetRequiredService<IGrainFactory>(),
                new RuntimeSessionArchiveStore(new RuntimePersistenceOptions
                {
                    RootDirectoryPath = rootDirectory,
                }),
                timeProvider,
            ],
            culture: null)!;
    }

    private static Microsoft.Agents.AI.Workflows.Workflow CreateNoCheckpointWorkflow()
    {
        var executor = new Microsoft.Agents.AI.Workflows.FunctionExecutor<string>(
            "no-checkpoint-executor",
            static async (input, context, cancellationToken) =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(input);
                cancellationToken.ThrowIfCancellationRequested();
                await context.RequestHaltAsync();
            },
            declareCrossRunShareable: true);
        return new Microsoft.Agents.AI.Workflows.WorkflowBuilder(executor).Build();
    }

    private static FileInfo CreateCheckpointFile(string rootDirectory, string fileName)
    {
        var filePath = Path.Combine(rootDirectory, fileName);
        File.WriteAllText(filePath, "{}");
        return new FileInfo(filePath);
    }

    private static T? InvokePrivateStatic<T>(string methodName, params object[] arguments)
    {
        var method = typeof(AgentFrameworkRuntimeClient).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        return (T?)method!.Invoke(null, arguments);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => timestamp;
}

internal sealed class TemporaryRuntimePersistenceDirectory : IDisposable
{
    public TemporaryRuntimePersistenceDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "dotpilot-runtime-tests", Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
