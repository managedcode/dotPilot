using DotPilot.Core.Features.AgentSessions;

namespace DotPilot.Presentation;

internal static class ChatComposerSendBehaviorText
{
    private const string EnterSendsTitle = "Press Enter to send";
    private const string EnterSendsSummary = "Use a modifier with Enter when you want a new line.";
    private const string EnterSendsHint = "Enter sends. Enter with a modifier adds a new line.";
    private const string EnterInsertsNewLineTitle = "Press Enter for a new line";
    private const string EnterInsertsNewLineSummary = "Use a modifier with Enter when you want to send.";
    private const string EnterInsertsNewLineHint = "Enter adds a new line. Enter with a modifier sends.";

    public static string GetTitle(ComposerSendBehavior behavior)
    {
        return behavior switch
        {
            ComposerSendBehavior.EnterSends => EnterSendsTitle,
            ComposerSendBehavior.EnterInsertsNewLine => EnterInsertsNewLineTitle,
            _ => EnterSendsTitle,
        };
    }

    public static string GetSummary(ComposerSendBehavior behavior)
    {
        return behavior switch
        {
            ComposerSendBehavior.EnterSends => EnterSendsSummary,
            ComposerSendBehavior.EnterInsertsNewLine => EnterInsertsNewLineSummary,
            _ => EnterSendsSummary,
        };
    }

    public static string GetHint(ComposerSendBehavior behavior)
    {
        return behavior switch
        {
            ComposerSendBehavior.EnterSends => EnterSendsHint,
            ComposerSendBehavior.EnterInsertsNewLine => EnterInsertsNewLineHint,
            _ => EnterSendsHint,
        };
    }
}
