using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;

namespace DotPilot.Core.ChatSessions;

internal sealed class LocalAgentSessionStateStore(AgentSessionStorageOptions storageOptions)
{
    private const string FileExtension = ".json";
    private const string TempSuffix = ".tmp";
    private readonly Dictionary<string, string> _memorySessions = [];

    public async ValueTask<AgentSession?> TryLoadAsync(
        AIAgent agent,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var path = GetPath(sessionId);
        string? payload;
        if (UseTransientStore())
        {
            payload = _memorySessions.GetValueOrDefault(path);
        }
        else
        {
            if (!File.Exists(path))
            {
                return null;
            }

            payload = await File.ReadAllTextAsync(path, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        return await agent.DeserializeSessionAsync(
            document.RootElement.Clone(),
            AgentSessionSerialization.Options,
            cancellationToken);
    }

    public async ValueTask SaveAsync(
        AIAgent agent,
        AgentSession session,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(session);

        var serialized = await agent.SerializeSessionAsync(
            session,
            AgentSessionSerialization.Options,
            cancellationToken);
        var path = GetPath(sessionId);
        var payload = serialized.GetRawText();
        if (UseTransientStore())
        {
            _memorySessions[path] = payload;
            return;
        }

        await WriteTextAsync(path, payload, cancellationToken);
    }

    public async ValueTask DeleteAsync(
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var path = GetPath(sessionId);
        if (UseTransientStore())
        {
            _memorySessions.Remove(path);
            return;
        }

        await LocalStorageDeletion.DeleteFileIfExistsAsync(path, cancellationToken);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        _memorySessions.Clear();
        if (UseTransientStore())
        {
            return;
        }

        await LocalStorageDeletion.DeleteDirectoryIfExistsAsync(
            AgentSessionStoragePaths.ResolveRuntimeSessionDirectory(storageOptions),
            cancellationToken);
    }

    private string GetPath(SessionId sessionId)
    {
        var directory = AgentSessionStoragePaths.ResolveRuntimeSessionDirectory(storageOptions);
        return Path.Combine(
            directory,
            sessionId.Value.ToString("N", CultureInfo.InvariantCulture) + FileExtension);
    }

    private static async ValueTask WriteTextAsync(
        string path,
        string payload,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        await File.WriteAllTextAsync(tempPath, payload, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }

    private bool UseTransientStore()
    {
        return storageOptions.UseInMemoryDatabase || OperatingSystem.IsBrowser();
    }
}
