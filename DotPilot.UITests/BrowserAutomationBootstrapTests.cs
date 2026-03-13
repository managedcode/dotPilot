using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotPilot.UITests;

[TestFixture]
public sealed class BrowserAutomationBootstrapTests
{
    private const string BrowserDriverEnvironmentVariableName = "UNO_UITEST_DRIVER_PATH";
    private const string BrowserBinaryEnvironmentVariableName = "UNO_UITEST_CHROME_BINARY_PATH";
    private const string BrowserPathEnvironmentVariableName = "UNO_UITEST_BROWSER_PATH";
    private const string ChromeDriverExecutableName = "chromedriver";
    private const string ChromeDriverExecutableNameWindows = "chromedriver.exe";
    private const string BrowserBinaryExecutableName = "chrome-under-test";
    private string? _originalBrowserDriverPath;
    private string? _originalBrowserBinaryPath;
    private string? _originalBrowserPath;

    [SetUp]
    public void CaptureOriginalEnvironment()
    {
        _originalBrowserDriverPath = Environment.GetEnvironmentVariable(BrowserDriverEnvironmentVariableName);
        _originalBrowserBinaryPath = Environment.GetEnvironmentVariable(BrowserBinaryEnvironmentVariableName);
        _originalBrowserPath = Environment.GetEnvironmentVariable(BrowserPathEnvironmentVariableName);
    }

    [TearDown]
    public void RestoreOriginalEnvironment()
    {
        Environment.SetEnvironmentVariable(BrowserDriverEnvironmentVariableName, _originalBrowserDriverPath);
        Environment.SetEnvironmentVariable(BrowserBinaryEnvironmentVariableName, _originalBrowserBinaryPath);
        Environment.SetEnvironmentVariable(BrowserPathEnvironmentVariableName, _originalBrowserPath);
    }

    [Test]
    public void WhenDriverPathPointsToBinaryThenResolverNormalizesToContainingDirectory()
    {
        using var sandbox = new BrowserAutomationSandbox();
        var driverFilePath = sandbox.CreateFile(GetChromeDriverExecutableFileName());
        var browserBinaryPath = sandbox.CreateFile(BrowserBinaryExecutableName);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [BrowserDriverEnvironmentVariableName] = driverFilePath,
            [BrowserBinaryEnvironmentVariableName] = browserBinaryPath,
        };

        var settings = BrowserAutomationBootstrap.Resolve(environment, []);

        Assert.That(settings.DriverPath, Is.EqualTo(Path.GetDirectoryName(driverFilePath)));
        Assert.That(settings.BrowserBinaryPath, Is.EqualTo(browserBinaryPath));
    }

    [Test]
    public void WhenBrowserBinaryEnvironmentVariableIsMissingThenResolverFallsBackToCandidatePaths()
    {
        using var sandbox = new BrowserAutomationSandbox();
        var driverFilePath = sandbox.CreateFile(GetChromeDriverExecutableFileName());
        var browserBinaryPath = sandbox.CreateFile(BrowserBinaryExecutableName);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [BrowserDriverEnvironmentVariableName] = driverFilePath,
            [BrowserBinaryEnvironmentVariableName] = null,
            [BrowserPathEnvironmentVariableName] = null,
        };

        var settings = BrowserAutomationBootstrap.Resolve(environment, [browserBinaryPath]);

        Assert.That(settings.DriverPath, Is.EqualTo(Path.GetDirectoryName(driverFilePath)));
        Assert.That(settings.BrowserBinaryPath, Is.EqualTo(browserBinaryPath));
    }

    [Test]
    public void WhenCachedDriverVersionMappingExistsThenResolverUsesCachedDriverDirectory()
    {
        using var sandbox = new BrowserAutomationSandbox();
        var browserBuild = "145.0.7632";
        var driverPlatform = GetExpectedDriverPlatform();
        var driverVersion = "145.0.7632.117";
        var cacheRootPath = sandbox.CreateDirectory("driver-cache");
        var driverDirectory = Path.Combine(cacheRootPath, driverVersion, $"chromedriver-{driverPlatform}");
        Directory.CreateDirectory(driverDirectory);
        sandbox.CreateFile(Path.Combine(driverDirectory, GetChromeDriverExecutableFileName()));
        BrowserAutomationBootstrap.PersistDriverVersionMapping(cacheRootPath, browserBuild, driverPlatform, driverVersion);

        var resolvedDirectory = BrowserAutomationBootstrap.ResolveCachedChromeDriverDirectory(
            cacheRootPath,
            browserBuild,
            driverPlatform);

        Assert.That(resolvedDirectory, Is.EqualTo(driverDirectory));
    }

    [Test]
    public void WhenAnyCachedDriverDirectoryExistsThenResolverUsesItWithoutVersionMapping()
    {
        using var sandbox = new BrowserAutomationSandbox();
        var cacheRootPath = sandbox.CreateDirectory("driver-cache");
        var driverDirectory = Path.Combine(cacheRootPath, "145.0.7632.117", $"chromedriver-{GetExpectedDriverPlatform()}");
        Directory.CreateDirectory(driverDirectory);
        sandbox.CreateFile(Path.Combine(driverDirectory, GetChromeDriverExecutableFileName()));

        var resolvedDirectory = BrowserAutomationBootstrap.ResolveAnyCachedChromeDriverDirectory(cacheRootPath);

        Assert.That(resolvedDirectory, Is.EqualTo(driverDirectory));
    }

    [Test]
    public void WhenVersionProbeProcessTimesOutThenItFailsFast()
    {
        var startInfo = CreateSleepStartInfo();

        var exception = Assert.Throws<TimeoutException>(
            () => BrowserAutomationBootstrap.RunProcessAndCaptureOutput(
                startInfo,
                TimeSpan.FromMilliseconds(50),
                "version probe timed out"));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("version probe timed out"));
    }

    private static string GetChromeDriverExecutableFileName()
    {
        return OperatingSystem.IsWindows()
            ? ChromeDriverExecutableNameWindows
            : ChromeDriverExecutableName;
    }

    private static string GetExpectedDriverPlatform()
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

        return RuntimeInformation.ProcessArchitecture == Architecture.X86
            ? "win32"
            : "win64";
    }

    private sealed class BrowserAutomationSandbox : IDisposable
    {
        private readonly string _rootPath = Path.Combine(
            Path.GetTempPath(),
            $"{nameof(BrowserAutomationBootstrapTests)}_{Guid.NewGuid():N}");

        public BrowserAutomationSandbox()
        {
            Directory.CreateDirectory(_rootPath);
        }

        public string CreateFile(string fileName)
        {
            var filePath = Path.Combine(_rootPath, fileName);
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(filePath, fileName);
            return filePath;
        }

        public string CreateDirectory(string relativePath)
        {
            var directoryPath = Path.Combine(_rootPath, relativePath);
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
    }

    private static ProcessStartInfo CreateSleepStartInfo()
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command Start-Sleep -Seconds 5",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }

        return new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = "-c \"sleep 5\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }
}
