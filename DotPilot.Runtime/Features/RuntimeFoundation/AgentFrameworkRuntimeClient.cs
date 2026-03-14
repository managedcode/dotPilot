using System.Text.Json;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeCommunication;
using DotPilot.Core.Features.RuntimeFoundation;
using ManagedCode.Communication;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class AgentFrameworkRuntimeClient : IAgentRuntimeClient
{
    private const string WorkflowName = "DotPilotRuntimeFoundationWorkflow";
    private const string WorkflowDescription =
        "Runs the local-first runtime flow with checkpointed pause and resume support for approval-gated sessions.";
    private const string ExecutorId = "runtime-foundation";
    private const string StateKey = "runtime-foundation-state";
    private const string StartReplayKind = "run-started";
    private const string PauseReplayKind = "approval-pending";
    private const string ResumeReplayKind = "run-resumed";
    private const string RejectedReplayKind = "approval-rejected";
    private const string CompletedReplayKind = "run-completed";
    private const string ResumeNotAllowedDetailFormat =
        "Session {0} is not paused with pending approval and cannot be resumed.";
    private static readonly System.Text.CompositeFormat ResumeNotAllowedDetailCompositeFormat =
        System.Text.CompositeFormat.Parse(ResumeNotAllowedDetailFormat);
    private readonly IGrainFactory _grainFactory;
    private readonly RuntimeSessionArchiveStore _archiveStore;
    private readonly DeterministicAgentTurnEngine _turnEngine;
    private readonly Workflow _workflow;
    private readonly TimeProvider _timeProvider;

    public AgentFrameworkRuntimeClient(IGrainFactory grainFactory, RuntimeSessionArchiveStore archiveStore)
        : this(grainFactory, archiveStore, TimeProvider.System)
    {
    }

    internal AgentFrameworkRuntimeClient(
        IGrainFactory grainFactory,
        RuntimeSessionArchiveStore archiveStore,
        TimeProvider timeProvider)
    {
        _grainFactory = grainFactory;
        _archiveStore = archiveStore;
        _timeProvider = timeProvider;
        _turnEngine = new DeterministicAgentTurnEngine(timeProvider);
        _workflow = BuildWorkflow();
    }

    public async ValueTask<Result<AgentTurnResult>> ExecuteAsync(AgentTurnRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validation = _turnEngine.Execute(request);
        if (validation.IsFailed)
        {
            return validation;
        }

        var checkpointDirectory = _archiveStore.CreateCheckpointDirectory(request.SessionId);
        using var checkpointStore = new FileSystemJsonCheckpointStore(checkpointDirectory);
        var checkpointManager = CheckpointManager.CreateJson(checkpointStore);
        await using var run = await InProcessExecution.RunAsync(
            _workflow,
            RuntimeWorkflowSignal.Start(request),
            checkpointManager,
            request.SessionId.ToString(),
            cancellationToken);

        var result = ExtractOutput(run);
        if (result is null)
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.OrchestrationUnavailable());
        }

        var checkpoint = await ResolveCheckpointAsync(run, checkpointDirectory, request.SessionId.ToString(), cancellationToken);
        await PersistRuntimeStateAsync(
            request,
            result,
            checkpoint,
            result.ApprovalState is ApprovalState.Pending ? PauseReplayKind : StartReplayKind,
            cancellationToken);

        return Result<AgentTurnResult>.Succeed(result);
    }

    public async ValueTask<Result<AgentTurnResult>> ResumeAsync(AgentTurnResumeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StoredRuntimeSessionArchive? archive;
        try
        {
            archive = await _archiveStore.LoadAsync(request.SessionId, cancellationToken);
        }
        catch (JsonException)
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.SessionArchiveCorrupted(request.SessionId));
        }

        if (archive is null)
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.SessionArchiveMissing(request.SessionId));
        }

        if (archive.Phase is not SessionPhase.Paused || archive.ApprovalState is not ApprovalState.Pending)
        {
            return Result<AgentTurnResult>.Fail(CreateResumeNotAllowedProblem(request.SessionId));
        }

        if (string.IsNullOrWhiteSpace(archive.CheckpointId))
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.ResumeCheckpointMissing(request.SessionId));
        }

        var checkpointDirectory = _archiveStore.CreateCheckpointDirectory(request.SessionId);
        using var checkpointStore = new FileSystemJsonCheckpointStore(checkpointDirectory);
        var checkpointManager = CheckpointManager.CreateJson(checkpointStore);
        await using var restoredRun = await InProcessExecution.ResumeAsync(
            _workflow,
            new CheckpointInfo(archive.WorkflowSessionId, archive.CheckpointId),
            checkpointManager,
            cancellationToken);
        _ = await restoredRun.ResumeAsync(cancellationToken, [RuntimeWorkflowSignal.Resume(request)]);

        var result = ExtractOutput(restoredRun);
        if (result is null)
        {
            return Result<AgentTurnResult>.Fail(RuntimeCommunicationProblems.OrchestrationUnavailable());
        }

        var resolvedCheckpoint =
            await ResolveCheckpointAsync(restoredRun, checkpointDirectory, archive.WorkflowSessionId, cancellationToken) ??
            new CheckpointInfo(archive.WorkflowSessionId, archive.CheckpointId);
        await PersistRuntimeStateAsync(
            archive.OriginalRequest,
            result,
            resolvedCheckpoint,
            ResolveResumeReplayKind(request.ApprovalState),
            cancellationToken);

        return Result<AgentTurnResult>.Succeed(result);
    }

    public async ValueTask<Result<RuntimeSessionArchive>> GetSessionArchiveAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var archive = await _archiveStore.LoadAsync(sessionId, cancellationToken);
            if (archive is null)
            {
                return Result<RuntimeSessionArchive>.Fail(RuntimeCommunicationProblems.SessionArchiveMissing(sessionId));
            }

            return Result<RuntimeSessionArchive>.Succeed(RuntimeSessionArchiveStore.ToSnapshot(archive));
        }
        catch (JsonException)
        {
            return Result<RuntimeSessionArchive>.Fail(RuntimeCommunicationProblems.SessionArchiveCorrupted(sessionId));
        }
    }

    private static AgentTurnResult? ExtractOutput(Run run)
    {
        return run.OutgoingEvents
            .OfType<WorkflowOutputEvent>()
            .Select(output => output.Data)
            .OfType<AgentTurnResult>()
            .LastOrDefault();
    }

    private static CheckpointInfo? ExtractCheckpoint(Run run)
    {
        var checkpoints = run.Checkpoints;
        return run.OutgoingEvents
            .OfType<SuperStepCompletedEvent>()
            .Select(step => step.CompletionInfo?.Checkpoint)
            .LastOrDefault(checkpoint => checkpoint is not null) ??
            run.LastCheckpoint ??
            (checkpoints.Count > 0 ? checkpoints[checkpoints.Count - 1] : null);
    }

    private static ValueTask<CheckpointInfo?> ResolveCheckpointAsync(
        Run run,
        DirectoryInfo checkpointDirectory,
        string workflowSessionId,
        CancellationToken cancellationToken)
    {
        return ResolveCheckpointCoreAsync(run, checkpointDirectory, workflowSessionId, cancellationToken);
    }

    private static async ValueTask<CheckpointInfo?> ResolveCheckpointCoreAsync(
        Run run,
        DirectoryInfo checkpointDirectory,
        string workflowSessionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var inMemoryCheckpoint = ExtractCheckpoint(run);
            if (inMemoryCheckpoint is not null)
            {
                return inMemoryCheckpoint;
            }

            var persistedCheckpoint = checkpointDirectory
                .EnumerateFiles($"{workflowSessionId}_*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => TryCreateCheckpointInfo(workflowSessionId, file))
                .FirstOrDefault(checkpoint => checkpoint is not null);
            if (persistedCheckpoint is not null)
            {
                return persistedCheckpoint;
            }

            var status = await run.GetStatusAsync(cancellationToken);
            if (status is not RunStatus.Running)
            {
                await Task.Yield();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        }

        return ExtractCheckpoint(run);
    }

    private static CheckpointInfo? TryCreateCheckpointInfo(string workflowSessionId, FileInfo file)
    {
        var fileName = Path.GetFileNameWithoutExtension(file.Name);
        var prefix = $"{workflowSessionId}_";
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var checkpointId = fileName[prefix.Length..];
        return string.IsNullOrWhiteSpace(checkpointId)
            ? null
            : new CheckpointInfo(workflowSessionId, checkpointId);
    }

    private async ValueTask PersistRuntimeStateAsync(
        AgentTurnRequest originalRequest,
        AgentTurnResult result,
        CheckpointInfo? checkpoint,
        string replayKind,
        CancellationToken cancellationToken)
    {
        var existingArchive = await _archiveStore.LoadAsync(originalRequest.SessionId, cancellationToken);
        var replay = existingArchive?.Replay.ToList() ?? [];
        var recordedAt = _timeProvider.GetUtcNow();
        replay.Add(
            new RuntimeSessionReplayEntry(
                replayKind,
                result.Summary,
                result.NextPhase,
                result.ApprovalState,
                recordedAt));
        if (result.NextPhase is SessionPhase.Execute or SessionPhase.Review or SessionPhase.Failed)
        {
            replay.Add(
                new RuntimeSessionReplayEntry(
                    CompletedReplayKind,
                    result.Summary,
                    result.NextPhase,
                    result.ApprovalState,
                    recordedAt));
        }

        var archive = new StoredRuntimeSessionArchive(
            originalRequest.SessionId,
            checkpoint?.SessionId ?? existingArchive?.WorkflowSessionId ?? originalRequest.SessionId.ToString(),
            checkpoint?.CheckpointId,
            originalRequest,
            result.NextPhase,
            result.ApprovalState,
            recordedAt,
            replay,
            result.ProducedArtifacts);

        await _archiveStore.SaveAsync(archive, cancellationToken);
        await UpsertSessionStateAsync(originalRequest, result, recordedAt);
        await UpsertArtifactsAsync(result.ProducedArtifacts);
    }

    private async ValueTask UpsertSessionStateAsync(AgentTurnRequest request, AgentTurnResult result, DateTimeOffset timestamp)
    {
        var session = new SessionDescriptor
        {
            Id = request.SessionId,
            WorkspaceId = new WorkspaceId(Guid.Empty),
            Title = request.Prompt,
            Phase = result.NextPhase,
            ApprovalState = result.ApprovalState,
            AgentProfileIds = [request.AgentProfileId],
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        };

        await _grainFactory.GetGrain<ISessionGrain>(request.SessionId.ToString()).UpsertAsync(session);
    }

    private async ValueTask UpsertArtifactsAsync(IReadOnlyList<ArtifactDescriptor> artifacts)
    {
        foreach (var artifact in artifacts)
        {
            await _grainFactory.GetGrain<IArtifactGrain>(artifact.Id.ToString()).UpsertAsync(artifact);
        }
    }

    private Workflow BuildWorkflow()
    {
        var runtimeExecutor = new FunctionExecutor<RuntimeWorkflowSignal>(
            ExecutorId,
            HandleSignalAsync,
            outputTypes: [typeof(AgentTurnResult)],
            declareCrossRunShareable: true);
        var builder = new WorkflowBuilder(runtimeExecutor)
            .WithName(WorkflowName)
            .WithDescription(WorkflowDescription)
            .WithOutputFrom(runtimeExecutor);
        return builder.Build();
    }

    private async ValueTask HandleSignalAsync(
        RuntimeWorkflowSignal signal,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var state = await context.ReadOrInitStateAsync(StateKey, static () => new RuntimeWorkflowState(), cancellationToken);

        switch (signal.Kind)
        {
            case RuntimeWorkflowSignalKind.Start:
                await HandleStartAsync(signal, context, cancellationToken);
                return;
            case RuntimeWorkflowSignalKind.Resume:
                await HandleResumeAsync(signal, state, context, cancellationToken);
                return;
            default:
                await context.RequestHaltAsync();
                return;
        }
    }

    private async ValueTask HandleStartAsync(
        RuntimeWorkflowSignal signal,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var request = signal.Request ?? throw new InvalidOperationException("Runtime workflow start requires an AgentTurnRequest.");
        var result = _turnEngine.Execute(request);
        if (result.IsFailed)
        {
            await context.RequestHaltAsync();
            return;
        }

        var output = result.Value!;
        await context.QueueStateUpdateAsync(
            StateKey,
            new RuntimeWorkflowState
            {
                OriginalRequest = request,
                ApprovalPending = output.ApprovalState is ApprovalState.Pending,
            },
            cancellationToken);
        await context.YieldOutputAsync(output, cancellationToken);
        await context.RequestHaltAsync();
    }

    private static string ResolveResumeReplayKind(ApprovalState approvalState)
    {
        return approvalState switch
        {
            ApprovalState.Approved => ResumeReplayKind,
            ApprovalState.Rejected => RejectedReplayKind,
            _ => PauseReplayKind,
        };
    }

    private static Problem CreateResumeNotAllowedProblem(SessionId sessionId)
    {
        return Problem.Create(
            RuntimeCommunicationProblemCode.ResumeCheckpointMissing,
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                ResumeNotAllowedDetailCompositeFormat,
                sessionId),
            (int)System.Net.HttpStatusCode.Conflict);
    }

    private async ValueTask HandleResumeAsync(
        RuntimeWorkflowSignal signal,
        RuntimeWorkflowState state,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        if (state.OriginalRequest is null)
        {
            await context.RequestHaltAsync();
            return;
        }

        var resumeRequest = signal.ResumeRequest ?? throw new InvalidOperationException("Runtime workflow resume requires an AgentTurnResumeRequest.");
        var resumedOutput = _turnEngine.Resume(state.OriginalRequest, resumeRequest);
        await context.QueueStateUpdateAsync(
            StateKey,
            state with
            {
                ApprovalPending = resumedOutput.ApprovalState is ApprovalState.Pending,
            },
            cancellationToken);
        await context.YieldOutputAsync(resumedOutput, cancellationToken);
        await context.RequestHaltAsync();
    }
}

internal enum RuntimeWorkflowSignalKind
{
    Start,
    Resume,
}

internal sealed record RuntimeWorkflowSignal(
    RuntimeWorkflowSignalKind Kind,
    AgentTurnRequest? Request,
    AgentTurnResumeRequest? ResumeRequest)
{
    public static RuntimeWorkflowSignal Start(AgentTurnRequest request) =>
        new(RuntimeWorkflowSignalKind.Start, request, null);

    public static RuntimeWorkflowSignal Resume(AgentTurnResumeRequest request) =>
        new(RuntimeWorkflowSignalKind.Resume, null, request);
}

internal sealed record RuntimeWorkflowState
{
    public AgentTurnRequest? OriginalRequest { get; init; }

    public bool ApprovalPending { get; init; }
}
