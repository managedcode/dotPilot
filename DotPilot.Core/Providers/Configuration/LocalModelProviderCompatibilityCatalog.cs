namespace DotPilot.Core.Providers;

internal static class LocalModelProviderCompatibilityCatalog
{
    private static readonly string[] OnnxRuntimeGenAiModelTypes =
    [
        "decoder",
        "llama",
        "mistral",
        "gemma",
        "gemma2",
        "gemma3",
        "gemma3_text",
        "granite",
        "phi",
        "phi3",
        "phi3small",
        "phi3v",
        "phi4mm",
        "phimoe",
        "qwen2",
        "qwen3",
        "chatglm",
        "nemotron",
        "olmo",
        "decoder-pipeline",
        "whisper",
        "marian-ssru",
    ];

    private static readonly string[] LlamaSharpArchitectures =
    [
        "llama",
        "mistral",
        "gemma",
        "gemma2",
        "gemma3",
        "gemma3n",
        "qwen2",
        "qwen2moe",
        "qwen2vl",
        "qwen3",
        "qwen3moe",
        "qwen3next",
        "qwen3vl",
        "qwen3vlmoe",
        "phi3",
        "phimoe",
        "granite",
        "granitemoe",
        "granitehybrid",
        "deepseek",
        "deepseek2",
        "command-r",
        "falcon",
        "falcon-h1",
        "starcoder",
        "starcoder2",
        "bert",
        "modern-bert",
        "neo-bert",
        "nomic-bert",
        "nomic-bert-moe",
        "jina-bert-v2",
        "jina-bert-v3",
        "llama4",
        "mistral3",
        "mamba",
        "mamba2",
        "olmo",
        "olmo2",
        "olmoe",
        "nemotron",
        "nemotron_h",
        "nemotron_h_moe",
        "exaone",
        "exaone4",
        "rwkv6",
        "rwkv6qwen2",
        "rwkv7",
        "arwkv7",
        "afmoe",
        "bailingmoe",
        "bailingmoe2",
        "glm4moe",
        "hunyuan-moe",
        "ernie4_5-moe",
        "llada-moe",
        "grovemoe",
        "lfm2moe",
    ];

    public static IReadOnlyList<string> GetSupportedRuntimeTypes(AgentProviderKind providerKind)
    {
        return providerKind switch
        {
            AgentProviderKind.Onnx => OnnxRuntimeGenAiModelTypes,
            AgentProviderKind.LlamaSharp => LlamaSharpArchitectures,
            _ => Array.Empty<string>(),
        };
    }

    public static string GetDetectedRuntimeTypeLabel(AgentProviderKind providerKind)
    {
        return providerKind switch
        {
            AgentProviderKind.Onnx => "Detected model type",
            AgentProviderKind.LlamaSharp => "Detected architecture",
            _ => "Detected type",
        };
    }

    public static string GetSupportedRuntimeTypesLabel(AgentProviderKind providerKind)
    {
        return providerKind switch
        {
            AgentProviderKind.Onnx => "Supported model types",
            AgentProviderKind.LlamaSharp => "Supported architectures",
            _ => "Supported types",
        };
    }
}
