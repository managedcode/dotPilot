using System.Text;
using System.Text.Json;
using DotPilot.Core.Features.ControlPlaneDomain;
using DotPilot.Core.Features.RuntimeFoundation;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class RuntimeSessionArchiveStore(RuntimePersistenceOptions options)
{
    private const string ArchiveFileName = "archive.json";
    private const string ReplayFileName = "replay.md";
    private const string CheckpointsDirectoryName = "checkpoints";
    private const string ReplayBulletPrefix = "- ";
    private const string ReplaySeparator = " | ";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    internal async ValueTask<StoredRuntimeSessionArchive?> LoadAsync(SessionId sessionId, CancellationToken cancellationToken)
    {
        var archivePath = GetArchivePath(sessionId);
        if (!File.Exists(archivePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(archivePath);
        return await JsonSerializer.DeserializeAsync<StoredRuntimeSessionArchive>(stream, SerializerOptions, cancellationToken);
    }

    internal async ValueTask SaveAsync(StoredRuntimeSessionArchive archive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var sessionDirectory = GetSessionDirectory(archive.SessionId);
        Directory.CreateDirectory(sessionDirectory);
        var archivePath = GetArchivePath(archive.SessionId);
        await using (var stream = File.Create(archivePath))
        {
            await JsonSerializer.SerializeAsync(stream, archive, SerializerOptions, cancellationToken);
        }

        await File.WriteAllTextAsync(
            GetReplayPath(archive.SessionId),
            BuildReplayMarkdown(archive.Replay),
            Encoding.UTF8,
            cancellationToken);
    }

    internal DirectoryInfo CreateCheckpointDirectory(SessionId sessionId)
    {
        var checkpointDirectory = GetCheckpointDirectoryPath(sessionId);
        Directory.CreateDirectory(checkpointDirectory);
        return new DirectoryInfo(checkpointDirectory);
    }

    internal static RuntimeSessionArchive ToSnapshot(StoredRuntimeSessionArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        return new RuntimeSessionArchive(
            archive.SessionId,
            archive.WorkflowSessionId,
            archive.Phase,
            archive.ApprovalState,
            archive.UpdatedAt,
            archive.CheckpointId,
            archive.Replay,
            archive.Artifacts);
    }

    private string GetSessionDirectory(SessionId sessionId)
    {
        return Path.Combine(options.RootDirectoryPath, sessionId.ToString());
    }

    private string GetArchivePath(SessionId sessionId)
    {
        return Path.Combine(GetSessionDirectory(sessionId), ArchiveFileName);
    }

    private string GetReplayPath(SessionId sessionId)
    {
        return Path.Combine(GetSessionDirectory(sessionId), ReplayFileName);
    }

    private string GetCheckpointDirectoryPath(SessionId sessionId)
    {
        return Path.Combine(GetSessionDirectory(sessionId), CheckpointsDirectoryName);
    }

    private static string BuildReplayMarkdown(IReadOnlyList<RuntimeSessionReplayEntry> replayEntries)
    {
        var builder = new StringBuilder();
        foreach (var entry in replayEntries)
        {
            _ = builder.Append(ReplayBulletPrefix)
                .Append(entry.RecordedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
                .Append(ReplaySeparator)
                .Append(entry.Kind)
                .Append(ReplaySeparator)
                .Append(entry.Phase)
                .Append(ReplaySeparator)
                .Append(entry.ApprovalState)
                .Append(ReplaySeparator)
                .AppendLine(entry.Summary);
        }

        return builder.ToString();
    }
}

internal sealed record StoredRuntimeSessionArchive(
    SessionId SessionId,
    string WorkflowSessionId,
    string? CheckpointId,
    AgentTurnRequest OriginalRequest,
    SessionPhase Phase,
    ApprovalState ApprovalState,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<RuntimeSessionReplayEntry> Replay,
    IReadOnlyList<ArtifactDescriptor> Artifacts);
