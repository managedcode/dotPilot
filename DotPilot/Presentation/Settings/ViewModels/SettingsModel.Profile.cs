namespace DotPilot.Presentation;

public partial record SettingsModel
{
    private const string ProfileSectionKey = "Profile";
    private const string DeleteAllDataRequestedMessage = "Confirm deletion to remove all local DotPilot data and reset the app.";
    private const string DeleteAllDataCancelledMessage = "Delete-all-data request cancelled.";
    private const string DeleteAllDataCompletedMessage = "All local DotPilot data was deleted and the app was reset to defaults.";
    private AsyncCommand? _requestDeleteAllDataCommand;
    private AsyncCommand? _confirmDeleteAllDataCommand;
    private AsyncCommand? _cancelDeleteAllDataCommand;

    public IState<bool> ShowDeleteAllDataConfirmation => State.Value(this, static () => false);

    public ICommand RequestDeleteAllDataCommand =>
        _requestDeleteAllDataCommand ??= new AsyncCommand(
            () => RequestDeleteAllData(CancellationToken.None));

    public ICommand ConfirmDeleteAllDataCommand =>
        _confirmDeleteAllDataCommand ??= new AsyncCommand(
            () => ConfirmDeleteAllData(CancellationToken.None));

    public ICommand CancelDeleteAllDataCommand =>
        _cancelDeleteAllDataCommand ??= new AsyncCommand(
            () => CancelDeleteAllData(CancellationToken.None));

    public async ValueTask RequestDeleteAllData(CancellationToken cancellationToken)
    {
        SettingsModelLog.DeleteAllDataRequested(logger);
        await ShowDeleteAllDataConfirmation.SetAsync(true, cancellationToken);
        await StatusMessage.SetAsync(DeleteAllDataRequestedMessage, cancellationToken);
    }

    public async ValueTask ConfirmDeleteAllData(CancellationToken cancellationToken)
    {
        try
        {
            SettingsModelLog.DeleteAllDataConfirmed(logger);
            var workspaceResult = await workspaceState.ResetWorkspaceAsync(cancellationToken);
            if (!workspaceResult.TryGetValue(out var workspace))
            {
                await ClearDeleteAllDataConfirmationAsync(cancellationToken);
                await StatusMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not delete local data."), cancellationToken);
                return;
            }

            var preferences = await operatorPreferencesStore.ResetAsync(cancellationToken);
            await SynchronizeComposerSendBehaviorAsync(preferences, cancellationToken);
            var providers = MapProviderStatusItems(workspace.Providers, selectedProvider: null);
            await EnsureSelectedProviderAsync(workspace, providers, cancellationToken);
            await ClearDeleteAllDataConfirmationAsync(cancellationToken);
            _workspaceRefresh.Raise();
            workspaceProjectionNotifier.Publish();
            await StatusMessage.SetAsync(DeleteAllDataCompletedMessage, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await ClearDeleteAllDataConfirmationAsync(cancellationToken);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    public async ValueTask CancelDeleteAllData(CancellationToken cancellationToken)
    {
        SettingsModelLog.DeleteAllDataCancelled(logger);
        await ClearDeleteAllDataConfirmationAsync(cancellationToken);
        await StatusMessage.SetAsync(DeleteAllDataCancelledMessage, cancellationToken);
    }

    private async ValueTask ClearDeleteAllDataConfirmationAsync(CancellationToken cancellationToken)
    {
        await ShowDeleteAllDataConfirmation.SetAsync(false, cancellationToken);
    }
}
