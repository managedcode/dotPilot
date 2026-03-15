namespace DotPilot.Core.LocalAgentHost;

internal static class LocalAgentHostStoragePaths
{
    private const string AppFolderName = "DotPilot";

    public static string ResolveStorageBasePath(LocalAgentHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.StorageBasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName)
            : options.StorageBasePath;
    }
}
