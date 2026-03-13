using System.IO.Enumeration;

namespace DotPilot.Runtime.Features.Workbench;

internal sealed class GitIgnoreRuleSet
{
    private const char CommentPrefix = '#';
    private const char NegationPrefix = '!';
    private const char DirectorySuffix = '/';
    private const char PathSeparator = '/';
    private const string GitIgnoreFileName = ".gitignore";

    private static readonly HashSet<string> AlwaysIgnoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".codex",
        ".git",
        ".vs",
        "bin",
        "obj",
        "TestResults",
    };

    private readonly IReadOnlyList<GitIgnorePattern> _patterns;

    private GitIgnoreRuleSet(IReadOnlyList<GitIgnorePattern> patterns)
    {
        _patterns = patterns;
    }

    public static GitIgnoreRuleSet Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var gitIgnorePath = Path.Combine(workspaceRoot, GitIgnoreFileName);
        if (!File.Exists(gitIgnorePath))
        {
            return new([]);
        }

        var patterns = File.ReadLines(gitIgnorePath)
            .Select(ParseLine)
            .OfType<GitIgnorePattern>()
            .ToArray();

        return new(patterns);
    }

    public bool IsIgnored(string relativePath, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var normalizedPath = Normalize(relativePath);
        var segments = normalizedPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => AlwaysIgnoredNames.Contains(segment)))
        {
            return true;
        }

        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(normalizedPath, segments, isDirectory))
            {
                return true;
            }
        }

        return false;
    }

    private static GitIgnorePattern? ParseLine(string rawLine)
    {
        var trimmed = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed[0] is CommentPrefix or NegationPrefix)
        {
            return null;
        }

        var directoryOnly = trimmed.EndsWith(DirectorySuffix);
        var rooted = trimmed.StartsWith(PathSeparator);
        var normalizedPattern = Normalize(trimmed.TrimStart(PathSeparator).TrimEnd(DirectorySuffix));
        if (string.IsNullOrWhiteSpace(normalizedPattern))
        {
            return null;
        }

        return new GitIgnorePattern(
            normalizedPattern,
            directoryOnly,
            rooted,
            normalizedPattern.Contains(PathSeparator));
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, PathSeparator)
            .Replace(Path.AltDirectorySeparatorChar, PathSeparator)
            .Trim();
    }

    private sealed record GitIgnorePattern(
        string Pattern,
        bool DirectoryOnly,
        bool Rooted,
        bool HasPathSeparator)
    {
        public bool IsMatch(string normalizedPath, IReadOnlyList<string> segments, bool isDirectory)
        {
            if (DirectoryOnly && !isDirectory)
            {
                return false;
            }

            if (Rooted)
            {
                return MatchesPath(normalizedPath);
            }

            if (HasPathSeparator)
            {
                return MatchesPath(normalizedPath) ||
                    normalizedPath.Contains(string.Concat(PathSeparator, Pattern), StringComparison.OrdinalIgnoreCase);
            }

            return segments.Any(segment => FileSystemName.MatchesSimpleExpression(Pattern, segment, ignoreCase: true));
        }

        private bool MatchesPath(string normalizedPath)
        {
            if (FileSystemName.MatchesSimpleExpression(Pattern, normalizedPath, ignoreCase: true))
            {
                return true;
            }

            return normalizedPath.Equals(Pattern, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(string.Concat(Pattern, PathSeparator), StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.EndsWith(string.Concat(PathSeparator, Pattern), StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Contains(string.Concat(PathSeparator, Pattern, PathSeparator), StringComparison.OrdinalIgnoreCase);
        }
    }
}
