using System.Text.Json;
using DotPilot.Core.ControlPlaneDomain;

namespace DotPilot.Core.ChatSessions;

internal sealed class LocalCodexThreadStateStore(AgentSessionStorageOptions storageOptions)
{
    private const string StateFileName = "codex-thread.json";
    private const string TempSuffix = ".tmp";
    private readonly Dictionary<string, LocalCodexThreadState> _memoryStates = [];

    public async ValueTask<LocalCodexThreadState?> TryLoadAsync(
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var path = GetPath(sessionId);
        if (UseTransientStore())
        {
            return _memoryStates.GetValueOrDefault(path);
        }

        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<LocalCodexThreadState>(
            stream,
            AgentSessionSerialization.Options,
            cancellationToken);
    }

    public async ValueTask SaveAsync(
        SessionId sessionId,
        LocalCodexThreadState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var path = GetPath(sessionId);
        if (UseTransientStore())
        {
            _memoryStates[path] = state;
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + TempSuffix;
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                state,
                AgentSessionSerialization.Options,
                cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public string ResolvePlaygroundDirectory(SessionId sessionId)
    {
        return AgentSessionStoragePaths.ResolvePlaygroundDirectory(storageOptions, sessionId);
    }

    private string GetPath(SessionId sessionId)
    {
        var directory = ResolvePlaygroundDirectory(sessionId);
        return Path.Combine(directory, StateFileName);
    }

    private bool UseTransientStore()
    {
        return storageOptions.UseInMemoryDatabase || OperatingSystem.IsBrowser();
    }
}
