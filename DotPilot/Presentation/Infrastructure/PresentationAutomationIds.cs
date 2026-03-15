namespace DotPilot.Presentation;

public static class PresentationAutomationIds
{
    private const char ReplacementCharacter = '-';
    private const string RepositoryNodePrefix = "RepositoryNode-";
    private const string RepositoryNodeTapPrefix = "RepositoryNodeTap-";
    private const string SettingsCategoryPrefix = "SettingsCategory-";
    private const string ToolchainProviderPrefix = "ToolchainProvider-";
    private const string ToolchainWorkstreamPrefix = "ToolchainWorkstream-";

    public static string RepositoryNode(string relativePath) => CreateScopedId(RepositoryNodePrefix, relativePath);

    public static string RepositoryNodeTap(string relativePath) => CreateScopedId(RepositoryNodeTapPrefix, relativePath);

    public static string SettingsCategory(string key) => CreateScopedId(SettingsCategoryPrefix, key);

    public static string ToolchainProvider(string commandName) => CreateScopedId(ToolchainProviderPrefix, commandName);

    public static string ToolchainWorkstream(int issueNumber) => CreateScopedId(ToolchainWorkstreamPrefix, issueNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string CreateScopedId(string prefix, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var sanitized = string.Concat(value.Select(static character =>
            char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : ReplacementCharacter));

        return string.Concat(prefix, sanitized.Trim(ReplacementCharacter));
    }
}
