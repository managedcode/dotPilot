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
            AgentProviderKind.Onnx => "Set the ONNX model directory path and refresh settings.",
            AgentProviderKind.LlamaSharp => "Set the GGUF model file path and refresh settings.",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    public static string GetLocalModelMissingSummary(this AgentProviderKind kind)
    {
        return kind switch
        {
            AgentProviderKind.Onnx => "ONNX model directory is not configured or is missing genai_config.json.",
            AgentProviderKind.LlamaSharp => "LLamaSharp GGUF model file is not configured or cannot be found.",
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
}
