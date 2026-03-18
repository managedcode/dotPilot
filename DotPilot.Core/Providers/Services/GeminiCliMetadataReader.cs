using ManagedCode.GeminiSharpSDK.Client;
using ManagedCode.GeminiSharpSDK.Configuration;

namespace DotPilot.Core.Providers;

internal static class GeminiCliMetadataReader
{
    public static ProviderCliMetadataSnapshot TryRead(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        try
        {
            using var client = new GeminiClient(new GeminiOptions
            {
                GeminiExecutablePath = executablePath,
            });

            var metadata = client.GetCliMetadata();
            var supportedModels = metadata.Models
                .Where(static model => model.IsListed)
                .Select(static model => model.Slug)
                .ToArray();

            return new ProviderCliMetadataSnapshot(
                metadata.InstalledVersion,
                ResolveSuggestedModel(metadata.DefaultModel, supportedModels),
                supportedModels);
        }
        catch
        {
            var fallbackModel = AgentProviderKind.Gemini.GetDefaultModelName();
            return new ProviderCliMetadataSnapshot(
                InstalledVersion: null,
                fallbackModel,
                AgentProviderKind.Gemini.GetSupportedModelNames());
        }
    }

    private static string ResolveSuggestedModel(string? configuredModel, IReadOnlyList<string> supportedModels)
    {
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel;
        }

        return supportedModels.FirstOrDefault(static model => !string.IsNullOrWhiteSpace(model)) ??
            AgentProviderKind.Gemini.GetDefaultModelName();
    }
}
