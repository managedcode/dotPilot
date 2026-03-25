using System.Text.Json;

namespace DotPilot.Core.Providers;

internal static class LocalModelProviderCompatibilityReader
{
    private const string OnnxConfigFileName = "genai_config.json";

    public static async ValueTask<LocalModelCompatibilityInfo> ReadAsync(
        AgentProviderKind providerKind,
        string? modelPath,
        CancellationToken cancellationToken)
    {
        return providerKind switch
        {
            AgentProviderKind.Onnx => await ReadOnnxCompatibilityAsync(modelPath, cancellationToken).ConfigureAwait(false),
            AgentProviderKind.LlamaSharp => await ReadLlamaSharpCompatibilityAsync(modelPath, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, null),
        };
    }

    private static async ValueTask<LocalModelCompatibilityInfo> ReadOnnxCompatibilityAsync(
        string? modelPath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizeOnnxPath(modelPath);
        var supportedTypes = LocalModelProviderCompatibilityCatalog.GetSupportedRuntimeTypes(AgentProviderKind.Onnx);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return CreateFailure(
                normalizedPath,
                supportedTypes,
                "MissingModelPath",
                "Choose an ONNX Runtime GenAI model folder or its genai_config.json file.");
        }

        if (!Directory.Exists(normalizedPath))
        {
            return CreateFailure(
                normalizedPath,
                supportedTypes,
                "MissingModelPath",
                "The selected ONNX Runtime GenAI model folder could not be found.");
        }

        var configPath = Path.Combine(normalizedPath, OnnxConfigFileName);
        if (!File.Exists(configPath))
        {
            return CreateFailure(
                normalizedPath,
                supportedTypes,
                "MissingOnnxConfig",
                "The selected ONNX model folder must contain genai_config.json.");
        }

        var onnxRead = await TryReadOnnxModelTypeAsync(configPath, cancellationToken).ConfigureAwait(false);
        if (!onnxRead.IsSuccess)
        {
            return CreateFailure(
                normalizedPath,
                supportedTypes,
                "InvalidOnnxConfig",
                onnxRead.ErrorMessage ?? "The selected genai_config.json could not be read.");
        }

        if (!supportedTypes.Contains(onnxRead.ModelType!, StringComparer.OrdinalIgnoreCase))
        {
            return CreateFailure(
                normalizedPath,
                supportedTypes,
                "UnsupportedOnnxModelType",
                $"ONNX Runtime GenAI model.type '{onnxRead.ModelType}' is not supported by dotPilot.",
                onnxRead.ModelType);
        }

        return new LocalModelCompatibilityInfo(
            normalizedPath,
            IsCompatible: true,
            SuggestedModelName: ResolveSuggestedModelName(normalizedPath),
            FailureCode: null,
            FailureMessage: null,
            DetectedRuntimeType: onnxRead.ModelType,
            supportedTypes);
    }

    private static async ValueTask<LocalModelCompatibilityInfo> ReadLlamaSharpCompatibilityAsync(
        string? modelPath,
        CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(modelPath);
        var supportedArchitectures = LocalModelProviderCompatibilityCatalog.GetSupportedRuntimeTypes(AgentProviderKind.LlamaSharp);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return CreateFailure(
                normalizedPath,
                supportedArchitectures,
                "MissingModelPath",
                "Choose a GGUF model file for LLamaSharp.");
        }

        if (!File.Exists(normalizedPath))
        {
            return CreateFailure(
                normalizedPath,
                supportedArchitectures,
                "MissingModelPath",
                "The selected LLamaSharp GGUF model file could not be found.");
        }

        if (!string.Equals(Path.GetExtension(normalizedPath), ".gguf", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFailure(
                normalizedPath,
                supportedArchitectures,
                "UnsupportedModelFile",
                "LLamaSharp requires a .gguf model file.");
        }

        var ggufRead = await GgufMetadataReader.TryReadArchitectureAsync(normalizedPath, cancellationToken).ConfigureAwait(false);
        if (!ggufRead.IsSuccess)
        {
            return CreateFailure(
                normalizedPath,
                supportedArchitectures,
                "InvalidGgufMetadata",
                ggufRead.ErrorMessage ?? "The selected GGUF model could not be read.");
        }

        if (!supportedArchitectures.Contains(ggufRead.Architecture!, StringComparer.OrdinalIgnoreCase))
        {
            return CreateFailure(
                normalizedPath,
                supportedArchitectures,
                "UnsupportedModelArchitecture",
                $"GGUF architecture '{ggufRead.Architecture}' is not supported by the bundled LLamaSharp backend.",
                ggufRead.Architecture);
        }

        return new LocalModelCompatibilityInfo(
            normalizedPath,
            IsCompatible: true,
            SuggestedModelName: ResolveSuggestedModelName(normalizedPath),
            FailureCode: null,
            FailureMessage: null,
            DetectedRuntimeType: ggufRead.Architecture,
            supportedArchitectures);
    }

    private static async ValueTask<(bool IsSuccess, string? ModelType, string? ErrorMessage)> TryReadOnnxModelTypeAsync(
        string configPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(configPath);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("model", out var modelElement))
            {
                return (false, null, "genai_config.json is missing the model section.");
            }

            if (!modelElement.TryGetProperty("type", out var typeElement) ||
                typeElement.ValueKind != JsonValueKind.String)
            {
                return (false, null, "genai_config.json is missing model.type.");
            }

            var modelType = typeElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(modelType))
            {
                return (false, null, "genai_config.json is missing model.type.");
            }

            return (true, modelType, null);
        }
        catch (JsonException)
        {
            return (false, null, "genai_config.json is not valid JSON.");
        }
        catch (IOException)
        {
            return (false, null, "genai_config.json could not be read.");
        }
    }

    private static LocalModelCompatibilityInfo CreateFailure(
        string? normalizedPath,
        IReadOnlyList<string> supportedTypes,
        string failureCode,
        string failureMessage,
        string? detectedRuntimeType = null)
    {
        return new LocalModelCompatibilityInfo(
            normalizedPath,
            IsCompatible: false,
            SuggestedModelName: ResolveSuggestedModelName(normalizedPath),
            FailureCode: failureCode,
            FailureMessage: failureMessage,
            DetectedRuntimeType: detectedRuntimeType,
            supportedTypes);
    }

    private static string? NormalizeOnnxPath(string? modelPath)
    {
        var normalizedPath = NormalizePath(modelPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return normalizedPath;
        }

        return File.Exists(normalizedPath) &&
               string.Equals(Path.GetFileName(normalizedPath), OnnxConfigFileName, StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(normalizedPath)
            : normalizedPath;
    }

    private static string? NormalizePath(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        var trimmedPath = modelPath.Trim();
        try
        {
            return Path.GetFullPath(trimmedPath);
        }
        catch (Exception)
        {
            return trimmedPath;
        }
    }

    private static string? ResolveSuggestedModelName(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        if (File.Exists(modelPath))
        {
            return Path.GetFileNameWithoutExtension(modelPath);
        }

        var trimmedPath = modelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmedPath);
    }
}
