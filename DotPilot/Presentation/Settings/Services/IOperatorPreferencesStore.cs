namespace DotPilot.Presentation;

public interface IOperatorPreferencesStore
{
    ValueTask<OperatorPreferencesSnapshot> GetAsync(CancellationToken cancellationToken);

    ValueTask<OperatorPreferencesSnapshot> SetAsync(
        ComposerSendBehavior behavior,
        CancellationToken cancellationToken);

    ValueTask<OperatorPreferencesSnapshot> ResetAsync(CancellationToken cancellationToken);
}
