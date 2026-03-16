using System.Text.Json;
using ManagedCode.CodexSharpSDK.Client;
using ManagedCode.CodexSharpSDK.Configuration;

namespace DotPilot.Core.Providers;

internal static class CodexCliMetadataReader
{
    private const string ConfigDirectoryName = ".codex";
    private const string ConfigFileName = "config.toml";
    private const string ModelsCacheFileName = "models_cache.json";
    private const string DefaultModelPropertyName = "model";
    private const string VersionSeparator = "version";

    public static CodexCliMetadataSnapshot? TryRead(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var fallbackSnapshot = TryReadFromLocalFiles();
        try
        {
            using var client = new CodexClient(new CodexOptions
            {
                CodexExecutablePath = executablePath,
            });
            var metadata = client.GetCliMetadata();
            return new CodexCliMetadataSnapshot(
                NormalizeInstalledVersion(metadata.InstalledVersion),
                ResolveDefaultModel(fallbackSnapshot?.DefaultModel, metadata.DefaultModel),
                MergeAvailableModels(
                    fallbackSnapshot?.AvailableModels ?? [],
                    metadata.Models
                        .Where(static model => model.IsListed)
                        .Select(static model => model.Slug)));
        }
        catch
        {
            return fallbackSnapshot;
        }
    }

    private static CodexCliMetadataSnapshot? TryReadFromLocalFiles()
    {
        var defaultModel = TryReadDefaultModelFromConfig();
        var models = TryReadAvailableModelsFromCache();
        return string.IsNullOrWhiteSpace(defaultModel) && models.Length == 0
            ? null
            : new CodexCliMetadataSnapshot(null, defaultModel, models);
    }

    private static string? NormalizeInstalledVersion(string? installedVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return installedVersion;
        }

        var firstLine = installedVersion
            .Split(Environment.NewLine, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return string.Empty;
        }

        var separatorIndex = firstLine.IndexOf(VersionSeparator, StringComparison.OrdinalIgnoreCase);
        return separatorIndex >= 0
            ? firstLine[(separatorIndex + VersionSeparator.Length)..].Trim(' ', ':')
            : firstLine.Trim();
    }

    private static string? ResolveDefaultModel(string? configuredModel, string? discoveredModel)
    {
        return string.IsNullOrWhiteSpace(configuredModel)
            ? discoveredModel
            : configuredModel;
    }

    private static string[] MergeAvailableModels(
        IReadOnlyList<string> configuredModels,
        IEnumerable<string> discoveredModels)
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<string> models = [];

        foreach (var model in configuredModels.Concat(discoveredModels))
        {
            if (string.IsNullOrWhiteSpace(model) || !seen.Add(model))
            {
                continue;
            }

            models.Add(model);
        }

        return [.. models];
    }

    private static string? TryReadDefaultModelFromConfig()
    {
        var configPath = ProviderCliHomeDirectory.GetFilePath(ConfigDirectoryName, ConfigFileName);
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines(configPath))
            {
                if (!TryParseConfigValue(line, DefaultModelPropertyName, out var value))
                {
                    continue;
                }

                return value;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string[] TryReadAvailableModelsFromCache()
    {
        var cachePath = ProviderCliHomeDirectory.GetFilePath(ConfigDirectoryName, ModelsCacheFileName);
        if (string.IsNullOrWhiteSpace(cachePath) || !File.Exists(cachePath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(cachePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("models", out var modelsElement) ||
                modelsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return modelsElement.EnumerateArray()
                .Select(static model => model.TryGetProperty("slug", out var slugProperty)
                    ? slugProperty.GetString()
                    : null)
                .Where(static slug => !string.IsNullOrWhiteSpace(slug))
                .Cast<string>()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool TryParseConfigValue(string line, string propertyName, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmedLine = line.Trim();
        if (!trimmedLine.StartsWith(propertyName, StringComparison.Ordinal) ||
            !trimmedLine.Contains('=', StringComparison.Ordinal))
        {
            return false;
        }

        var separatorIndex = trimmedLine.IndexOf('=', StringComparison.Ordinal);
        var candidate = trimmedLine[(separatorIndex + 1)..].Trim();
        if (candidate.StartsWith('"') && candidate.EndsWith('"') && candidate.Length >= 2)
        {
            candidate = candidate[1..^1];
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }
}

internal sealed record CodexCliMetadataSnapshot(
    string? InstalledVersion,
    string? DefaultModel,
    IReadOnlyList<string> AvailableModels);
