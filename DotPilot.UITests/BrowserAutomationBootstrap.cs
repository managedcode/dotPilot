using System.Collections.ObjectModel;

namespace DotPilot.UITests;

internal static class BrowserAutomationBootstrap
{
    private const string BrowserDriverEnvironmentVariableName = "UNO_UITEST_DRIVER_PATH";
    private const string BrowserBinaryEnvironmentVariableName = "UNO_UITEST_CHROME_BINARY_PATH";
    private const string BrowserPathEnvironmentVariableName = "UNO_UITEST_BROWSER_PATH";
    private const string ChromeDriverExecutableName = "chromedriver";
    private const string ChromeDriverExecutableNameWindows = "chromedriver.exe";
    private const string ChromeExecutableNameLinux = "google-chrome";
    private const string ChromeStableExecutableNameLinux = "google-chrome-stable";
    private const string ChromiumExecutableNameLinux = "chromium";
    private const string ChromiumBrowserExecutableNameLinux = "chromium-browser";
    private const string WindowsChromeRelativePath = @"Google\Chrome\Application\chrome.exe";
    private const string WindowsChromeLocalAppDataRelativePath = @"Google\Chrome\Application\chrome.exe";
    private const string MacChromeBinaryPath = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
    private const string MacChromeForTestingBinaryPath =
        "/Applications/Google Chrome for Testing.app/Contents/MacOS/Google Chrome for Testing";
    private const string BrowserBinaryNotFoundMessage =
        "Unable to locate a Chrome browser binary for DotPilot UI smoke tests. " +
        "Set UNO_UITEST_CHROME_BINARY_PATH or UNO_UITEST_BROWSER_PATH explicitly.";
    private const string SearchedLocationsLabel = "Searched locations:";
    private static readonly ReadOnlyCollection<string> DefaultBrowserBinaryCandidates =
        CreateDefaultBrowserBinaryCandidates();

    public static BrowserAutomationSettings Resolve()
    {
        return Resolve(CreateEnvironmentSnapshot(), DefaultBrowserBinaryCandidates);
    }

    internal static BrowserAutomationSettings Resolve(
        IReadOnlyDictionary<string, string?> environment,
        IReadOnlyList<string> browserBinaryCandidates)
    {
        var driverPath = NormalizeBrowserDriverPath(environment);
        var browserBinaryPath = ResolveBrowserBinaryPath(environment, browserBinaryCandidates);
        SetEnvironmentVariableIfMissing(BrowserBinaryEnvironmentVariableName, browserBinaryPath, environment);
        SetEnvironmentVariableIfMissing(BrowserPathEnvironmentVariableName, browserBinaryPath, environment);

        return new BrowserAutomationSettings(driverPath, browserBinaryPath);
    }

    private static string? NormalizeBrowserDriverPath(IReadOnlyDictionary<string, string?> environment)
    {
        if (!environment.TryGetValue(BrowserDriverEnvironmentVariableName, out var configuredPath) ||
            string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        if (File.Exists(configuredPath))
        {
            var directory = Path.GetDirectoryName(configuredPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Environment.SetEnvironmentVariable(BrowserDriverEnvironmentVariableName, directory);
                return directory;
            }
        }

        if (Directory.Exists(configuredPath))
        {
            var driverPath = Path.Combine(configuredPath, GetChromeDriverExecutableFileName());
            if (File.Exists(driverPath))
            {
                return configuredPath;
            }
        }

        return null;
    }

    private static string ResolveBrowserBinaryPath(
        IReadOnlyDictionary<string, string?> environment,
        IReadOnlyList<string> browserBinaryCandidates)
    {
        foreach (var environmentVariableName in GetBrowserBinaryEnvironmentVariableNames())
        {
            if (environment.TryGetValue(environmentVariableName, out var configuredPath) &&
                !string.IsNullOrWhiteSpace(configuredPath) &&
                File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        foreach (var candidatePath in browserBinaryCandidates)
        {
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        var searchedLocations = browserBinaryCandidates.Count == 0
            ? " none"
            : $"{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", browserBinaryCandidates)}";

        throw new InvalidOperationException($"{BrowserBinaryNotFoundMessage}{Environment.NewLine}{SearchedLocationsLabel}{searchedLocations}");
    }

    private static Dictionary<string, string?> CreateEnvironmentSnapshot()
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [BrowserDriverEnvironmentVariableName] = Environment.GetEnvironmentVariable(BrowserDriverEnvironmentVariableName),
            [BrowserBinaryEnvironmentVariableName] = Environment.GetEnvironmentVariable(BrowserBinaryEnvironmentVariableName),
            [BrowserPathEnvironmentVariableName] = Environment.GetEnvironmentVariable(BrowserPathEnvironmentVariableName),
        };
    }

    private static void SetEnvironmentVariableIfMissing(
        string environmentVariableName,
        string value,
        IReadOnlyDictionary<string, string?> environment)
    {
        if (environment.TryGetValue(environmentVariableName, out var configuredValue) &&
            !string.IsNullOrWhiteSpace(configuredValue))
        {
            return;
        }

        Environment.SetEnvironmentVariable(environmentVariableName, value);
    }

    private static ReadOnlyCollection<string> CreateDefaultBrowserBinaryCandidates()
    {
        var candidates = new List<string>();

        if (OperatingSystem.IsMacOS())
        {
            candidates.Add(MacChromeBinaryPath);
            candidates.Add(MacChromeForTestingBinaryPath);
            return candidates.AsReadOnly();
        }

        if (OperatingSystem.IsLinux())
        {
            candidates.Add(Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", ChromeExecutableNameLinux));
            candidates.Add(Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", ChromeStableExecutableNameLinux));
            candidates.Add(Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", ChromiumExecutableNameLinux));
            candidates.Add(Path.Combine(Path.DirectorySeparatorChar.ToString(), "usr", "bin", ChromiumBrowserExecutableNameLinux));
            return candidates.AsReadOnly();
        }

        if (OperatingSystem.IsWindows())
        {
            AddWindowsBrowserCandidate(candidates, Environment.SpecialFolder.ProgramFiles, WindowsChromeRelativePath);
            AddWindowsBrowserCandidate(candidates, Environment.SpecialFolder.ProgramFilesX86, WindowsChromeRelativePath);
            AddWindowsBrowserCandidate(candidates, Environment.SpecialFolder.LocalApplicationData, WindowsChromeLocalAppDataRelativePath);
        }

        return candidates.AsReadOnly();
    }

    private static void AddWindowsBrowserCandidate(
        List<string> candidates,
        Environment.SpecialFolder specialFolder,
        string relativePath)
    {
        var rootPath = Environment.GetFolderPath(specialFolder);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        candidates.Add(Path.Combine(rootPath, relativePath));
    }

    private static string GetChromeDriverExecutableFileName()
    {
        return OperatingSystem.IsWindows()
            ? ChromeDriverExecutableNameWindows
            : ChromeDriverExecutableName;
    }

    private static IEnumerable<string> GetBrowserBinaryEnvironmentVariableNames()
    {
        yield return BrowserBinaryEnvironmentVariableName;
        yield return BrowserPathEnvironmentVariableName;
    }
}

internal sealed record BrowserAutomationSettings(string? DriverPath, string BrowserBinaryPath);
