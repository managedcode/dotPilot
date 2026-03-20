using System.Text.Json;

namespace DotPilot.Presentation;

public sealed class LocalOperatorPreferencesStore(OperatorPreferencesStorageOptions storageOptions)
    : IOperatorPreferencesStore, IDisposable
{
    private const string PreferencesDirectoryName = "dotPilot";
    private const string PreferencesFileName = "operator-preferences.json";
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<OperatorPreferencesSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await LoadFromDiskAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<OperatorPreferencesSnapshot> SetAsync(
        ComposerSendBehavior behavior,
        CancellationToken cancellationToken)
    {
        var snapshot = new OperatorPreferencesSnapshot(behavior);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await SaveToDiskAsync(snapshot, cancellationToken);
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<OperatorPreferencesSnapshot> ResetAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var filePath = ResolvePreferencesFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory) &&
                Directory.Exists(directory) &&
                !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }

            return CreateDefaultSnapshot();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async ValueTask<OperatorPreferencesSnapshot> LoadFromDiskAsync(CancellationToken cancellationToken)
    {
        var filePath = ResolvePreferencesFilePath();
        if (!File.Exists(filePath))
        {
            return CreateDefaultSnapshot();
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var dto = JsonSerializer.Deserialize(
                json,
                OperatorPreferencesJsonSerializerContext.Default.OperatorPreferencesDto);
            return dto?.ToSnapshot() ?? CreateDefaultSnapshot();
        }
        catch
        {
            return CreateDefaultSnapshot();
        }
    }

    private async ValueTask SaveToDiskAsync(
        OperatorPreferencesSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var filePath = ResolvePreferencesFilePath();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var dto = OperatorPreferencesDto.FromSnapshot(snapshot);
        var json = JsonSerializer.Serialize(
            dto,
            OperatorPreferencesJsonSerializerContext.Default.OperatorPreferencesDto);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private string ResolvePreferencesFilePath()
    {
        if (!string.IsNullOrWhiteSpace(storageOptions.FilePath))
        {
            return storageOptions.FilePath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, ".local")
            : localAppData;
        return Path.Combine(baseDirectory, PreferencesDirectoryName, PreferencesFileName);
    }

    private static OperatorPreferencesSnapshot CreateDefaultSnapshot()
    {
        return new OperatorPreferencesSnapshot(ComposerSendBehavior.EnterSends);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}

internal sealed record OperatorPreferencesDto(string ComposerSendBehavior)
{
    public OperatorPreferencesSnapshot ToSnapshot()
    {
        return Enum.TryParse<global::DotPilot.Presentation.ComposerSendBehavior>(
                ComposerSendBehavior,
                ignoreCase: true,
                out var behavior)
            ? new OperatorPreferencesSnapshot(behavior)
            : new OperatorPreferencesSnapshot(global::DotPilot.Presentation.ComposerSendBehavior.EnterSends);
    }

    public static OperatorPreferencesDto FromSnapshot(OperatorPreferencesSnapshot snapshot)
    {
        return new OperatorPreferencesDto(snapshot.ComposerSendBehavior.ToString());
    }
}
