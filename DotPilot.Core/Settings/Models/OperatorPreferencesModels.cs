namespace DotPilot.Core.Settings.Models;

public enum ComposerSendBehavior
{
    EnterSends = 0,
    EnterInsertsNewLine = 1,
}

public sealed record OperatorPreferencesSnapshot(
    ComposerSendBehavior ComposerSendBehavior);
