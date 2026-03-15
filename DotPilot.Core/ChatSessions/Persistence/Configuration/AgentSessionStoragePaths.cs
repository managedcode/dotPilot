namespace DotPilot.Core.ChatSessions;

internal static class AgentSessionStoragePaths
{
    private const string AppFolderName = "DotPilot";
    private const string DatabaseFileName = "dotpilot-agent-sessions.db";
    private const string RuntimeSessionsFolderName = "agent-runtime-sessions";
    private const string ChatHistoryFolderName = "agent-chat-history";

    public static string ResolveDatabasePath(AgentSessionStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.DatabasePath))
        {
            return options.DatabasePath;
        }

        return Path.Combine(GetAppStorageRoot(), DatabaseFileName);
    }

    public static string ResolveRuntimeSessionDirectory(AgentSessionStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.RuntimeSessionDirectoryPath)
            ? Path.Combine(GetAppStorageRoot(), RuntimeSessionsFolderName)
            : options.RuntimeSessionDirectoryPath;
    }

    public static string ResolveChatHistoryDirectory(AgentSessionStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.ChatHistoryDirectoryPath)
            ? Path.Combine(GetAppStorageRoot(), ChatHistoryFolderName)
            : options.ChatHistoryDirectoryPath;
    }

    private static string GetAppStorageRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);
    }
}
