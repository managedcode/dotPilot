using Windows.Storage.Pickers;

namespace DotPilot.Presentation;

internal sealed class DesktopLocalModelPathPicker : ILocalModelPathPicker
{
    private static readonly SemaphoreSlim PickerBaseDirectoryGate = new(1, 1);

    public async ValueTask<LocalModelPathPickerResult> PickAsync(
        AgentProviderKind providerKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsBrowser())
        {
            return new LocalModelPathPickerResult(
                null,
                IsCancelled: false,
                ErrorMessage: "Local model selection is available only in the desktop app.");
        }

        try
        {
            return providerKind switch
            {
                AgentProviderKind.Onnx => await PickOnnxConfigAsync(cancellationToken),
                AgentProviderKind.LlamaSharp => await PickGgufFileAsync(cancellationToken),
                _ => new LocalModelPathPickerResult(
                    null,
                    IsCancelled: false,
                    ErrorMessage: $"{GetProviderDisplayName(providerKind)} does not use a local model path."),
            };
        }
        catch (Exception exception)
        {
            return new LocalModelPathPickerResult(null, IsCancelled: false, ErrorMessage: exception.Message);
        }
    }

    private static async ValueTask<LocalModelPathPickerResult> PickOnnxConfigAsync(CancellationToken cancellationToken)
    {
        await PickerBaseDirectoryGate.WaitAsync(cancellationToken);
        var originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            TryUsePickerBaseDirectory();

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            TryInitializeForWindow(picker);

            var file = await picker.PickSingleFileAsync();
            if (string.IsNullOrWhiteSpace(file?.Path))
            {
                return new LocalModelPathPickerResult(null, IsCancelled: true);
            }

            if (!string.Equals(file.Name, "genai_config.json", StringComparison.OrdinalIgnoreCase))
            {
                return new LocalModelPathPickerResult(
                    null,
                    IsCancelled: false,
                    ErrorMessage: "Choose the ONNX Runtime GenAI genai_config.json file from the model folder.");
            }

            var modelDirectory = Path.GetDirectoryName(file.Path);
            return string.IsNullOrWhiteSpace(modelDirectory)
                ? new LocalModelPathPickerResult(null, IsCancelled: true)
                : new LocalModelPathPickerResult(modelDirectory, IsCancelled: false);
        }
        finally
        {
            TryRestoreCurrentDirectory(originalCurrentDirectory);
            PickerBaseDirectoryGate.Release();
        }
    }

    private static async ValueTask<LocalModelPathPickerResult> PickGgufFileAsync(CancellationToken cancellationToken)
    {
        await PickerBaseDirectoryGate.WaitAsync(cancellationToken);
        var originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            TryUsePickerBaseDirectory();

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".gguf");
            TryInitializeForWindow(picker);

            var file = await picker.PickSingleFileAsync();
            return string.IsNullOrWhiteSpace(file?.Path)
                ? new LocalModelPathPickerResult(null, IsCancelled: true)
                : new LocalModelPathPickerResult(file.Path, IsCancelled: false);
        }
        finally
        {
            TryRestoreCurrentDirectory(originalCurrentDirectory);
            PickerBaseDirectoryGate.Release();
        }
    }

    private static void TryInitializeForWindow(object picker)
    {
        if (!OperatingSystem.IsWindows() || App.DesktopWindow is null)
        {
            return;
        }

        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.DesktopWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
    }

    private static string GetProviderDisplayName(AgentProviderKind providerKind)
    {
        return providerKind switch
        {
            AgentProviderKind.Onnx => "ONNX Runtime GenAI",
            AgentProviderKind.LlamaSharp => "LLamaSharp",
            AgentProviderKind.Codex => "Codex",
            AgentProviderKind.ClaudeCode => "Claude Code",
            AgentProviderKind.GitHubCopilot => "GitHub Copilot",
            AgentProviderKind.Gemini => "Gemini",
            AgentProviderKind.Debug => "Debug Provider",
            _ => providerKind.ToString(),
        };
    }

    private static void TryUsePickerBaseDirectory()
    {
        var baseDirectory = ResolvePickerBaseDirectory();
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            return;
        }

        Environment.CurrentDirectory = baseDirectory;
    }

    private static void TryRestoreCurrentDirectory(string originalCurrentDirectory)
    {
        if (string.IsNullOrWhiteSpace(originalCurrentDirectory) || !Directory.Exists(originalCurrentDirectory))
        {
            return;
        }

        Environment.CurrentDirectory = originalCurrentDirectory;
    }

    private static string ResolvePickerBaseDirectory()
    {
        var currentDirectory = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            return currentDirectory;
        }

        return AppContext.BaseDirectory;
    }
}
