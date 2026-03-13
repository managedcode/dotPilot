using System.Runtime.InteropServices;
using DotPilot.Core.Features.ControlPlaneDomain;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

internal sealed class ProviderToolchainProbe
{
    private const string MissingStatusSummaryFormat = "{0} is not on PATH.";
    private const string AvailableStatusSummaryFormat = "{0} is available on PATH.";
    private static readonly System.Text.CompositeFormat MissingStatusSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(MissingStatusSummaryFormat);
    private static readonly System.Text.CompositeFormat AvailableStatusSummaryCompositeFormat =
        System.Text.CompositeFormat.Parse(AvailableStatusSummaryFormat);

    public static ProviderDescriptor Probe(string displayName, string commandName, bool requiresExternalToolchain)
    {
        var status = ResolveExecutablePath(commandName) is null
            ? ProviderConnectionStatus.Unavailable
            : ProviderConnectionStatus.Available;
        var statusSummary = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            status is ProviderConnectionStatus.Available
                ? AvailableStatusSummaryCompositeFormat
                : MissingStatusSummaryCompositeFormat,
            displayName);

        return new ProviderDescriptor
        {
            Id = ProviderId.New(),
            DisplayName = displayName,
            CommandName = commandName,
            Status = status,
            StatusSummary = statusSummary,
            RequiresExternalToolchain = requiresExternalToolchain,
        };
    }

    private static string? ResolveExecutablePath(string commandName)
    {
        var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var searchPath in searchPaths)
        {
            foreach (var candidate in EnumerateCandidates(searchPath, commandName))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidates(string searchPath, string commandName)
    {
        yield return Path.Combine(searchPath, commandName);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield break;
        }

        foreach (var extension in (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(searchPath, string.Concat(commandName, extension));
        }
    }
}
