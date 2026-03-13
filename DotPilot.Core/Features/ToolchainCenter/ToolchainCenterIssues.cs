namespace DotPilot.Core.Features.ToolchainCenter;

public static class ToolchainCenterIssues
{
    public const int ToolchainCenterEpic = 14;
    public const int ToolchainCenterUi = 33;
    public const int CodexReadiness = 34;
    public const int ClaudeCodeReadiness = 35;
    public const int GitHubCopilotReadiness = 36;
    public const int ConnectionDiagnostics = 37;
    public const int ProviderConfiguration = 38;
    public const int BackgroundPolling = 39;

    private const string IssueLabelFormat = "ISSUE #{0}";
    private static readonly System.Text.CompositeFormat IssueLabelCompositeFormat =
        System.Text.CompositeFormat.Parse(IssueLabelFormat);

    public static string FormatIssueLabel(int issueNumber) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture, IssueLabelCompositeFormat, issueNumber);
}
