namespace DotPilot.Runtime.Host.Features.AgentSessions;

internal static class AgentSessionHostStoragePaths
{
    private const string AppFolderName = "DotPilot";

    public static string ResolveStorageBasePath(AgentSessionHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.StorageBasePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName)
            : options.StorageBasePath;
    }
}
