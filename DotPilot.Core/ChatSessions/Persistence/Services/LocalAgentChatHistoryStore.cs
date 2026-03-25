using System.Text.Json;
using Microsoft.Extensions.AI;

namespace DotPilot.Core.ChatSessions;

internal sealed class LocalAgentChatHistoryStore(AgentSessionStorageOptions storageOptions)
{
    private const string FileExtension = ".json";
    private const string TempSuffix = ".tmp";
    private readonly Dictionary<string, ChatMessage[]> _memoryHistory = [];

    public async ValueTask<IReadOnlyList<ChatMessage>> LoadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var path = GetPath(storageKey);
        if (UseTransientStore())
        {
            return _memoryHistory.GetValueOrDefault(path) ?? [];
        }

        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        var messages = await JsonSerializer.DeserializeAsync<ChatMessage[]>(
            stream,
            AgentSessionSerialization.Options,
            cancellationToken);

        return messages ?? [];
    }

    public async ValueTask AppendAsync(
        string storageKey,
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var existing = await LoadAsync(storageKey, cancellationToken);
        var combined = existing.Concat(messages).ToArray();
        var path = GetPath(storageKey);
        if (UseTransientStore())
        {
            _memoryHistory[path] = combined;
            return;
        }

        await WriteAsync(path, combined, cancellationToken);
    }

    public async ValueTask DeleteAsync(
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var storageKey = sessionId.Value.ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var path = GetPath(storageKey);
        if (UseTransientStore())
        {
            _memoryHistory.Remove(path);
            return;
        }

        await LocalStorageDeletion.DeleteFileIfExistsAsync(path, cancellationToken);
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        _memoryHistory.Clear();
        if (UseTransientStore())
        {
            return;
        }

        await LocalStorageDeletion.DeleteDirectoryIfExistsAsync(
            AgentSessionStoragePaths.ResolveChatHistoryDirectory(storageOptions),
            cancellationToken);
    }

    private string GetPath(string storageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        var directory = AgentSessionStoragePaths.ResolveChatHistoryDirectory(storageOptions);
        return Path.Combine(directory, storageKey + FileExtension);
    }

    private static async ValueTask WriteAsync(
        string path,
        ChatMessage[] payload,
        CancellationToken cancellationToken)
    {
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
                payload,
                AgentSessionSerialization.Options,
                cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private bool UseTransientStore()
    {
        return storageOptions.UseInMemoryDatabase || OperatingSystem.IsBrowser();
    }
}
