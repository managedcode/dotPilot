namespace DotPilot.Runtime.Features.RuntimeFoundation;

public sealed class RuntimePersistenceOptions
{
    private const string DotPilotDirectoryName = "dotPilot";
    private const string RuntimeDirectoryName = "runtime";
    private const string SessionsDirectoryName = "sessions";

    public string RootDirectoryPath { get; init; } = CreateDefaultRootDirectoryPath();

    public static string CreateDefaultRootDirectoryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DotPilotDirectoryName,
            RuntimeDirectoryName,
            SessionsDirectoryName);
    }
}
