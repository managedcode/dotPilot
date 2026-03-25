namespace DotPilot.Core.Providers;

internal static class AgentProviderKindLocalModelExtensions
{
    public static bool IsLocalModelProvider(this AgentProviderKind kind)
    {
        return kind is AgentProviderKind.Onnx or AgentProviderKind.LlamaSharp;
    }

    public static IReadOnlyList<string> GetModelPathEnvironmentVariableNames(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => ["DOTPILOT_ONNX_MODEL_PATH", "ONNX_MODEL_PATH"],
            AgentProviderKind.LlamaSharp => ["DOTPILOT_LLAMASHARP_MODEL_PATH", "LLAMASHARP_MODEL_PATH"],
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetPrimaryModelPathEnvironmentVariableName(this AgentProviderKind kind)
    {
        return kind.GetModelPathEnvironmentVariableNames()[0];
    }

    public static string GetModelPathSetupCommand(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "DOTPILOT_ONNX_MODEL_PATH=/absolute/path/to/onnx-model-directory",
            AgentProviderKind.LlamaSharp => "DOTPILOT_LLAMASHARP_MODEL_PATH=/absolute/path/to/model.gguf",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelSetupSummary(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "Add a model by choosing its genai_config.json file and dotPilot will validate the containing ONNX Runtime GenAI folder.",
            AgentProviderKind.LlamaSharp => "Add a GGUF model file and dotPilot will validate its GGUF architecture before saving it.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelPickerLabel(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "Add genai_config.json",
            AgentProviderKind.LlamaSharp => "Add model file",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static ProviderActionKind GetLocalModelPickerActionKind(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => ProviderActionKind.PickFile,
            AgentProviderKind.LlamaSharp => ProviderActionKind.PickFile,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelMissingSummary(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "ONNX model path is not configured or the selected folder is not a supported ONNX Runtime GenAI model.",
            AgentProviderKind.LlamaSharp => "LLamaSharp GGUF model file is not configured or the selected GGUF architecture is not supported by the bundled backend.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelReadySummary(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "Local ONNX model is ready for desktop execution.",
            AgentProviderKind.LlamaSharp => "Local LLamaSharp model is ready for desktop execution.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelDetectedRuntimeTypeLabel(this AgentProviderKind kind)
    {
        return LocalModelProviderCompatibilityCatalog.GetDetectedRuntimeTypeLabel(kind);
    }

    public static string GetLocalModelSupportedRuntimeTypesLabel(this AgentProviderKind kind)
    {
        return LocalModelProviderCompatibilityCatalog.GetSupportedRuntimeTypesLabel(kind);
    }
}
