namespace DotPilot.Core.Features.Workbench;

public static class WorkbenchIssues
{
    private const string IssuePrefix = "#";

    public const int DesktopWorkbenchEpic = 13;
    public const int PrimaryShell = 28;
    public const int RepositoryTree = 29;
    public const int DocumentSurface = 30;
    public const int ArtifactDock = 31;
    public const int SettingsShell = 32;

    public static string FormatIssueLabel(int issueNumber) => string.Concat(IssuePrefix, issueNumber);
}
