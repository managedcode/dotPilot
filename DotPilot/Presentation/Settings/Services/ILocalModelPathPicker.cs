namespace DotPilot.Presentation;

public interface ILocalModelPathPicker
{
    ValueTask<LocalModelPathPickerResult> PickAsync(
        AgentProviderKind providerKind,
        CancellationToken cancellationToken);
}

public sealed record LocalModelPathPickerResult(
    string? SelectedPath,
    bool IsCancelled,
    string? ErrorMessage = null);
