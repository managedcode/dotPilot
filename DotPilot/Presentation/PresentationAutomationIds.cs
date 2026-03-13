namespace DotPilot.Presentation;

public static class PresentationAutomationIds
{
    private const char ReplacementCharacter = '-';
    private const string RepositoryNodePrefix = "RepositoryNode-";
    private const string SettingsCategoryPrefix = "SettingsCategory-";

    public static string RepositoryNode(string relativePath) => CreateScopedId(RepositoryNodePrefix, relativePath);

    public static string SettingsCategory(string key) => CreateScopedId(SettingsCategoryPrefix, key);

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
