namespace DotPilot.Core.Providers;

internal static class LocalModelProviderConfigurationReader
{
    public static LocalModelProviderConfiguration Read(AgentProviderKind providerKind)
    {
        if (!providerKind.IsLocalModelProvider())
        {
            throw new ArgumentOutOfRangeException(nameof(providerKind), providerKind, null);
        }

        var environmentVariableNames = providerKind.GetModelPathEnvironmentVariableNames();
        var modelPath = ResolveModelPath(environmentVariableNames);
        return new LocalModelProviderConfiguration(
            providerKind.GetPrimaryModelPathEnvironmentVariableName(),
            environmentVariableNames,
            providerKind.GetModelPathSetupCommand(),
            modelPath,
            IsReady(providerKind, modelPath),
            ResolveSuggestedModelName(modelPath));
    }

    private static string? ResolveModelPath(IReadOnlyList<string> environmentVariableNames)
    {
        foreach (var environmentVariableName in environmentVariableNames)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool IsReady(AgentProviderKind providerKind, string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return false;
        }

        return providerKind switch
        {
            AgentProviderKind.Onnx => Directory.Exists(modelPath) &&
                File.Exists(Path.Combine(modelPath, "genai_config.json")),
            AgentProviderKind.LlamaSharp => File.Exists(modelPath),
            _ => false,
        };
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
