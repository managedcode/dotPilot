using Windows.ApplicationModel.DataTransfer;

namespace DotPilot.Presentation;

public partial record SettingsModel
{
    public async ValueTask ExecuteProviderAction(ProviderActionItem? action, CancellationToken cancellationToken)
    {
        if (action is null)
        {
            return;
        }

        try
        {
            switch (action.Kind)
            {
                case ProviderActionKind.PickFile:
                case ProviderActionKind.PickFolder:
                    await ExecuteLocalModelPickerActionAsync(cancellationToken);
                    break;
                case ProviderActionKind.CopyCommand:
                default:
                    await CopyProviderActionCommandAsync(action, cancellationToken);
                    break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SettingsModelLog.Failure(logger, exception);
            await StatusMessage.SetAsync(exception.Message, cancellationToken);
        }
    }

    private async ValueTask ExecuteLocalModelPickerActionAsync(CancellationToken cancellationToken)
    {
        var selectedProvider = (await SelectedProvider) ?? EmptySelectedProvider;
        if (IsEmptySelectedProvider(selectedProvider))
        {
            return;
        }

        var pickerResult = await localModelPathPicker.PickAsync(selectedProvider.Kind, cancellationToken);
        if (pickerResult.IsCancelled)
        {
            await StatusMessage.SetAsync("Model selection cancelled.", cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(pickerResult.ErrorMessage))
        {
            await StatusMessage.SetAsync(pickerResult.ErrorMessage, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(pickerResult.SelectedPath))
        {
            await StatusMessage.SetAsync("No local model path was selected.", cancellationToken);
            return;
        }

        var updatedResult = await workspaceState.SetLocalModelPathAsync(
            new SetLocalModelPathCommand(selectedProvider.Kind, pickerResult.SelectedPath),
            cancellationToken);
        if (!updatedResult.TryGetValue(out _))
        {
            await StatusMessage.SetAsync(updatedResult.ToOperatorMessage("Could not save the local model path."), cancellationToken);
            return;
        }

        var workspaceResult = await workspaceState.GetWorkspaceAsync(cancellationToken);
        if (!workspaceResult.TryGetValue(out var workspace))
        {
            await StatusMessage.SetAsync(workspaceResult.ToOperatorMessage("Could not reload provider readiness."), cancellationToken);
            return;
        }

        var providers = MapProviderStatusItems(workspace.Providers, selectedProvider: null);
        await EnsureSelectedProviderAsync(workspace, providers, cancellationToken);
        _workspaceRefresh.Raise();
        workspaceProjectionNotifier.Publish();
        await StatusMessage.SetAsync($"{selectedProvider.DisplayName} model added.", cancellationToken);
    }

    private async ValueTask CopyProviderActionCommandAsync(
        ProviderActionItem action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action.Command))
        {
            await StatusMessage.SetAsync(action.Summary, cancellationToken);
            return;
        }

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(action.Command);
            Clipboard.SetContent(dataPackage);
            Clipboard.Flush();
            await StatusMessage.SetAsync($"Copied command: {action.Command}", cancellationToken);
        }
        catch (Exception)
        {
            await StatusMessage.SetAsync($"Run this command in your terminal: {action.Command}", cancellationToken);
        }
    }
}
