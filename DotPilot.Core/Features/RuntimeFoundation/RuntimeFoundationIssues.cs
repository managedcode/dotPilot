namespace DotPilot.Core.Features.RuntimeFoundation;

public static class RuntimeFoundationIssues
{
    private const string IssuePrefix = "#";

    public const int EmbeddedAgentRuntimeHostEpic = 12;
    public const int DomainModel = 22;
    public const int CommunicationContracts = 23;
    public const int EmbeddedOrleansHost = 24;
    public const int AgentFrameworkRuntime = 25;

    public static string FormatIssueLabel(int issueNumber) => string.Concat(IssuePrefix, issueNumber);
}
