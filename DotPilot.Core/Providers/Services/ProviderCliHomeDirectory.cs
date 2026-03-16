namespace DotPilot.Core.Providers;

internal static class ProviderCliHomeDirectory
{
    public static string GetPath()
    {
        foreach (var variableName in VariableNames)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public static string GetFilePath(string directoryName, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var homePath = GetPath();
        return string.IsNullOrWhiteSpace(homePath)
            ? string.Empty
            : Path.Combine(homePath, directoryName, fileName);
    }

    private static readonly string[] VariableNames =
    [
        "HOME",
        "USERPROFILE",
    ];
}
