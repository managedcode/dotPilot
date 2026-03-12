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
        var browserBinaryPath = sandbox.CreateFile(BrowserBinaryExecutableName);
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [BrowserDriverEnvironmentVariableName] = null,
            [BrowserBinaryEnvironmentVariableName] = null,
            [BrowserPathEnvironmentVariableName] = null,
        };

        var settings = BrowserAutomationBootstrap.Resolve(environment, [browserBinaryPath]);

        Assert.That(settings.DriverPath, Is.Null);
        Assert.That(settings.BrowserBinaryPath, Is.EqualTo(browserBinaryPath));
    }

    private static string GetChromeDriverExecutableFileName()
    {
        return OperatingSystem.IsWindows()
            ? ChromeDriverExecutableNameWindows
            : ChromeDriverExecutableName;
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
            File.WriteAllText(filePath, fileName);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_rootPath))
            {
                Directory.Delete(_rootPath, recursive: true);
            }
        }
    }
}
