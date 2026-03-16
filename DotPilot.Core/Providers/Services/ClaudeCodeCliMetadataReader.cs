using System.Text.Json;
using ManagedCode.ClaudeCodeSharpSDK.Client;
using ManagedCode.ClaudeCodeSharpSDK.Configuration;

namespace DotPilot.Core.Providers;

internal static class ClaudeCodeCliMetadataReader
{
    private const string SettingsFileName = "settings.json";
    private const string SuggestedModelPropertyName = "model";

    public static ProviderCliMetadataSnapshot TryRead(string executablePath, AgentSessionProviderProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(profile);

        var configuredModel = ReadSuggestedModelFromSettings();
        try
        {
            using var client = new ClaudeClient(new ClaudeOptions
            {
                ClaudeExecutablePath = executablePath,
            });

            var metadata = client.GetCliMetadata();
            return new ProviderCliMetadataSnapshot(
                metadata.InstalledVersion,
                ResolveSuggestedModel(configuredModel, metadata.DefaultModel),
                metadata.Models
                    .Where(static model => model.IsListed)
                    .Select(static model => model.Slug)
                    .ToArray());
        }
        catch
        {
            return new ProviderCliMetadataSnapshot(
                InstalledVersion: null,
                configuredModel,
                profile.SupportedModelNames);
        }
    }

    private static string? ResolveSuggestedModel(string? configuredModel, string? defaultModel)
    {
        return string.IsNullOrWhiteSpace(configuredModel)
            ? defaultModel
            : configuredModel;
    }

    private static string? ReadSuggestedModelFromSettings()
    {
        var settingsPath = GetSettingsPath();
        if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty(SuggestedModelPropertyName, out var property)
                ? property.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetSettingsPath()
    {
        return ProviderCliHomeDirectory.GetFilePath(".claude", SettingsFileName);
    }
}
