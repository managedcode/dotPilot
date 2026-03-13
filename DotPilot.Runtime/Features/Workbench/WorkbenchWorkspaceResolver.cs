namespace DotPilot.Runtime.Features.Workbench;

internal static class WorkbenchWorkspaceResolver
{
    private const string SolutionFileName = "DotPilot.slnx";
    private const string GitDirectoryName = ".git";

    public static ResolvedWorkspace Resolve(string? workspaceRootOverride)
    {
        if (!string.IsNullOrWhiteSpace(workspaceRootOverride) &&
            Directory.Exists(workspaceRootOverride))
        {
            return CreateResolvedWorkspace(workspaceRootOverride);
        }

        if (OperatingSystem.IsBrowser())
        {
            return ResolvedWorkspace.Unavailable;
        }

        foreach (var candidate in GetCandidateDirectories())
        {
            var resolvedRoot = FindWorkspaceRoot(candidate);
            if (resolvedRoot is not null)
            {
                return CreateResolvedWorkspace(resolvedRoot);
            }
        }

        return ResolvedWorkspace.Unavailable;
    }

    private static IEnumerable<string> GetCandidateDirectories()
    {
        return new[]
        {
            Environment.CurrentDirectory,
            AppContext.BaseDirectory,
        }
        .Where(static candidate => !string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
        .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? FindWorkspaceRoot(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, SolutionFileName)) ||
                Directory.Exists(Path.Combine(current.FullName, GitDirectoryName)))
            {
                return current.FullName;
            }
        }

        return null;
    }

    private static ResolvedWorkspace CreateResolvedWorkspace(string workspaceRoot)
    {
        var workspaceName = new DirectoryInfo(workspaceRoot).Name;
        return new ResolvedWorkspace(workspaceRoot, workspaceName, IsAvailable: true);
    }
}

internal sealed record ResolvedWorkspace(
    string Root,
    string Name,
    bool IsAvailable)
{
    public static ResolvedWorkspace Unavailable { get; } = new(string.Empty, string.Empty, IsAvailable: false);
}
