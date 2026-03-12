using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotPilot.UITests;

internal static partial class BrowserAutomationBootstrap
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
    private const string BrowserVersionArgument = "--version";
    private const string BrowserVersionPattern = @"(\d+\.\d+\.\d+\.\d+)";
    private const string BrowserBinaryNotFoundMessage =
        "Unable to locate a Chrome browser binary for DotPilot UI smoke tests. " +
        "Set UNO_UITEST_CHROME_BINARY_PATH or UNO_UITEST_BROWSER_PATH explicitly.";
    private const string DriverPlatformNotSupportedMessage =
        "DotPilot UI smoke tests do not have an automatic ChromeDriver mapping for the current operating system and architecture.";
    private const string BrowserVersionNotFoundMessage =
        "Unable to determine the installed Chrome version for DotPilot UI smoke tests.";
    private const string DriverVersionNotFoundMessage =
        "Unable to determine a matching ChromeDriver version for the installed Chrome build.";
    private const string DriverDownloadFailedMessage =
        "Failed to download the ChromeDriver archive required for DotPilot UI smoke tests.";
    private const string DriverExecutableNotFoundMessage =
        "ChromeDriver bootstrap completed without producing the expected executable.";
    private const string DriverCacheDirectoryName = "dotpilot-uitest-drivers";
    private const string ChromeDriverBundleNamePrefix = "chromedriver-";
    private const string LatestPatchVersionsUrl =
        "https://googlechromelabs.github.io/chrome-for-testing/latest-patch-versions-per-build.json";
    private const string ChromeForTestingDownloadBaseUrl =
        "https://storage.googleapis.com/chrome-for-testing-public";
    private const string BuildsPropertyName = "builds";
    private const string VersionPropertyName = "version";
    private const string SearchedLocationsLabel = "Searched locations:";
    private static readonly ReadOnlyCollection<string> DefaultBrowserBinaryCandidates =
        CreateDefaultBrowserBinaryCandidates();
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2),
    };

    public static BrowserAutomationSettings Resolve()
    {
        return Resolve(CreateEnvironmentSnapshot(), DefaultBrowserBinaryCandidates, applyEnvironmentVariables: true);
    }

    internal static BrowserAutomationSettings Resolve(
        IReadOnlyDictionary<string, string?> environment,
        IReadOnlyList<string> browserBinaryCandidates,
        bool applyEnvironmentVariables = false)
    {
        var browserBinaryPath = ResolveBrowserBinaryPath(environment, browserBinaryCandidates);
        var driverPath = ResolveBrowserDriverPath(environment, browserBinaryPath);

        if (applyEnvironmentVariables)
        {
            SetEnvironmentVariableIfMissing(BrowserBinaryEnvironmentVariableName, browserBinaryPath, environment);
            SetEnvironmentVariableIfMissing(BrowserPathEnvironmentVariableName, browserBinaryPath, environment);
            SetEnvironmentVariableIfMissing(BrowserDriverEnvironmentVariableName, driverPath, environment);
        }

        return new BrowserAutomationSettings(driverPath, browserBinaryPath);
    }

    private static string ResolveBrowserDriverPath(
        IReadOnlyDictionary<string, string?> environment,
        string browserBinaryPath)
    {
        var configuredDriverPath = NormalizeBrowserDriverPath(environment);
        return !string.IsNullOrWhiteSpace(configuredDriverPath)
            ? configuredDriverPath
            : EnsureChromeDriverDownloaded(browserBinaryPath);
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

    private static string EnsureChromeDriverDownloaded(string browserBinaryPath)
    {
        var browserVersion = ResolveBrowserVersion(browserBinaryPath);
        var driverVersion = ResolveChromeDriverVersion(browserVersion);
        var driverPlatform = ResolveChromeDriverPlatform();
        var cacheRootPath = Path.Combine(Path.GetTempPath(), DriverCacheDirectoryName, driverVersion);
        var driverDirectory = Path.Combine(cacheRootPath, $"{ChromeDriverBundleNamePrefix}{driverPlatform}");
        var driverExecutablePath = Path.Combine(driverDirectory, GetChromeDriverExecutableFileName());

        if (File.Exists(driverExecutablePath))
        {
            EnsureDriverExecutablePermissions(driverExecutablePath);
            return driverDirectory;
        }

        Directory.CreateDirectory(cacheRootPath);
        DownloadChromeDriverArchive(driverVersion, driverPlatform, cacheRootPath);
        EnsureDriverExecutablePermissions(driverExecutablePath);

        if (!File.Exists(driverExecutablePath))
        {
            throw new InvalidOperationException($"{DriverExecutableNotFoundMessage} Expected path: {driverExecutablePath}");
        }

        return driverDirectory;
    }

    private static void DownloadChromeDriverArchive(string driverVersion, string driverPlatform, string cacheRootPath)
    {
        var archiveName = $"{ChromeDriverBundleNamePrefix}{driverPlatform}.zip";
        var archivePath = Path.Combine(cacheRootPath, archiveName);
        var driverDirectory = Path.Combine(cacheRootPath, $"{ChromeDriverBundleNamePrefix}{driverPlatform}");

        if (Directory.Exists(driverDirectory))
        {
            Directory.Delete(driverDirectory, recursive: true);
        }

        var downloadUrl = BuildChromeDriverDownloadUrl(driverVersion, driverPlatform, archiveName);
        var archiveBytes = GetResponseBytes(downloadUrl, DriverDownloadFailedMessage);
        File.WriteAllBytes(archivePath, archiveBytes);
        ZipFile.ExtractToDirectory(archivePath, cacheRootPath, overwriteFiles: true);
    }

    private static byte[] GetResponseBytes(string requestUri, string failureMessage)
    {
        try
        {
            return HttpClient.GetByteArrayAsync(requestUri).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{failureMessage} Source: {requestUri}", exception);
        }
    }

    private static string ResolveBrowserVersion(string browserBinaryPath)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = browserBinaryPath,
            Arguments = BrowserVersionArgument,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException(BrowserVersionNotFoundMessage);

        var output = $"{process.StandardOutput.ReadToEnd()}{Environment.NewLine}{process.StandardError.ReadToEnd()}";
        process.WaitForExit();

        var match = BrowserVersionRegex().Match(output);
        if (!match.Success)
        {
            throw new InvalidOperationException($"{BrowserVersionNotFoundMessage} Output: {output.Trim()}");
        }

        return match.Groups[1].Value;
    }

    private static string ResolveChromeDriverVersion(string browserVersion)
    {
        var browserBuild = BuildChromeVersionKey(browserVersion);
        var response = GetResponseBytes(LatestPatchVersionsUrl, DriverVersionNotFoundMessage);
        using var document = JsonDocument.Parse(response);

        if (!document.RootElement.TryGetProperty(BuildsPropertyName, out var buildsElement) ||
            !buildsElement.TryGetProperty(browserBuild, out var buildElement) ||
            !buildElement.TryGetProperty(VersionPropertyName, out var versionElement))
        {
            throw new InvalidOperationException($"{DriverVersionNotFoundMessage} Browser build: {browserBuild}");
        }

        return versionElement.GetString()
            ?? throw new InvalidOperationException($"{DriverVersionNotFoundMessage} Browser build: {browserBuild}");
    }

    private static string BuildChromeVersionKey(string browserVersion)
    {
        var segments = browserVersion.Split('.');
        if (segments.Length < 3)
        {
            throw new InvalidOperationException($"{BrowserVersionNotFoundMessage} Parsed version: {browserVersion}");
        }

        return string.Join('.', segments.Take(3));
    }

    private static string ResolveChromeDriverPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "mac-arm64"
                : "mac-x64";
        }

        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "linux64";
        }

        if (OperatingSystem.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.X86
                ? "win32"
                : "win64";
        }

        throw new PlatformNotSupportedException(DriverPlatformNotSupportedMessage);
    }

    private static string BuildChromeDriverDownloadUrl(
        string driverVersion,
        string driverPlatform,
        string archiveName)
    {
        return $"{ChromeForTestingDownloadBaseUrl}/{driverVersion}/{driverPlatform}/{archiveName}";
    }

    private static void EnsureDriverExecutablePermissions(string driverExecutablePath)
    {
        if (!File.Exists(driverExecutablePath) || OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            driverExecutablePath,
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherExecute);
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

    [GeneratedRegex(BrowserVersionPattern, RegexOptions.CultureInvariant)]
    private static partial Regex BrowserVersionRegex();
}

internal sealed record BrowserAutomationSettings(string DriverPath, string BrowserBinaryPath);
