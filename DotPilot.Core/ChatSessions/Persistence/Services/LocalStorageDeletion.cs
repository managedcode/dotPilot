namespace DotPilot.Core.ChatSessions;

internal static class LocalStorageDeletion
{
    private const int DeleteRetryCount = 5;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(100);

    public static async Task DeleteFileIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay, cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay, cancellationToken);
            }
        }
    }

    public static async Task DeleteDirectoryIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < DeleteRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay, cancellationToken);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount - 1)
            {
                await Task.Delay(DeleteRetryDelay, cancellationToken);
            }
        }
    }
}
