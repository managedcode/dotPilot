using ManagedCode.CodexSharpSDK.Client;
using ManagedCode.CodexSharpSDK.Configuration;

namespace DotPilot.Core.Providers;

internal static class CodexCliMetadataReader
{
    private const string VersionSeparator = "version";

    public static CodexCliMetadataSnapshot? TryRead(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        try
        {
            using var client = new CodexClient(new CodexOptions
            {
                CodexExecutablePath = executablePath,
            });
            var metadata = client.GetCliMetadata();
            return new CodexCliMetadataSnapshot(
                NormalizeInstalledVersion(metadata.InstalledVersion),
                metadata.DefaultModel,
                metadata.Models
                    .Where(static model => model.IsListed)
                    .Select(static model => model.Slug)
                    .ToArray());
        }
        catch
        {
            return null;
        }
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
}

internal sealed record CodexCliMetadataSnapshot(
    string? InstalledVersion,
    string? DefaultModel,
    IReadOnlyList<string> AvailableModels);
