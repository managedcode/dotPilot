using System.Text.Json;
using GitHub.Copilot.SDK;

namespace DotPilot.Core.Providers;

internal static class CopilotCliMetadataReader
{
    private const string ConfigFileName = "config.json";
    private const string SuggestedModelPropertyName = "model";
    private const string EnabledPolicyState = "enabled";
    private const string ModelSettingHeader = "`model`:";

    public static async ValueTask<ProviderCliMetadataSnapshot> TryReadAsync(
        string executablePath,
        AgentSessionProviderProfile profile,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(profile);

        var configuredModel = ReadConfiguredModel();
        try
        {
            return await ReadViaSdkAsync(executablePath, configuredModel, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return new ProviderCliMetadataSnapshot(
                InstalledVersion: null,
                configuredModel,
                ReadSupportedModelsFromHelp(executablePath, profile.SupportedModelNames));
        }
    }

    private static async ValueTask<ProviderCliMetadataSnapshot> ReadViaSdkAsync(
        string executablePath,
        string? configuredModel,
        CancellationToken cancellationToken)
    {
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            CliPath = executablePath,
            AutoStart = false,
            UseStdio = true,
        });

        await client.StartAsync(cancellationToken).ConfigureAwait(false);
        var status = await client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        var models = await client.ListModelsAsync(cancellationToken).ConfigureAwait(false);

        return new ProviderCliMetadataSnapshot(
            status.Version,
            configuredModel,
            models
                .Where(static model => string.IsNullOrWhiteSpace(model.Policy?.State) ||
                    string.Equals(model.Policy.State, EnabledPolicyState, StringComparison.OrdinalIgnoreCase))
                .Select(static model => model.Id)
                .ToArray());
    }

    private static string? ReadConfiguredModel()
    {
        var configPath = GetConfigPath();
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(configPath);
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

    private static IReadOnlyList<string> ReadSupportedModelsFromHelp(string executablePath, IReadOnlyList<string> fallbackModels)
    {
        var helpOutput = AgentSessionCommandProbe.ReadOutput(executablePath, ["help", "config"]);
        if (string.IsNullOrWhiteSpace(helpOutput))
        {
            return fallbackModels;
        }

        List<string> models = [];
        var inModelSection = false;
        foreach (var rawLine in helpOutput.Split(Environment.NewLine, StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (!inModelSection)
            {
                inModelSection = string.Equals(line, ModelSettingHeader, StringComparison.Ordinal);
                continue;
            }

            if (line.StartsWith('`'))
            {
                break;
            }

            if (!line.StartsWith('-') &&
                !line.StartsWith('`'))
            {
                continue;
            }

            var model = line.TrimStart('-', ' ')
                .Trim('"');
            if (!string.IsNullOrWhiteSpace(model))
            {
                models.Add(model);
            }
        }

        return models.Count == 0 ? fallbackModels : models;
    }

    private static string GetConfigPath()
    {
        return ProviderCliHomeDirectory.GetFilePath(".copilot", ConfigFileName);
    }
}
