
namespace DotPilot.UITests;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1810:Initialize reference type static fields inline",
    Justification = "UI smoke tests need one-time browser host and driver bootstrap before test execution.")]
public class TestBase
{
    private const string BrowserDriverEnvironmentVariableName = "UNO_UITEST_DRIVER_PATH";
    private const string BrowserDriverFileName = "chromedriver";
    private const string ShowBrowserEnvironmentVariableName = "DOTPILOT_UITEST_SHOW_BROWSER";
    private const string MissingBrowserDriverMessage =
        "Browser UI smoke requires UNO_UITEST_DRIVER_PATH to point to a ChromeDriver binary.";
    private const int BrowserWindowWidth = 1440;
    private const int BrowserWindowHeight = 960;
    private static readonly object BrowserAppSyncRoot = new();

    private static IApp? _browserApp;
    private static readonly bool _browserHeadless = ResolveBrowserHeadless();
    private IApp? _app;

    static TestBase()
    {
        var browserDriverPath = NormalizeBrowserDriverPath();
        if (Constants.CurrentPlatform == Platform.Browser &&
            !string.IsNullOrWhiteSpace(browserDriverPath))
        {
            BrowserTestHost.EnsureStarted(Constants.WebAssemblyDefaultUri);
        }

        AppInitializer.TestEnvironment.AndroidAppName = Constants.AndroidAppName;
        AppInitializer.TestEnvironment.WebAssemblyDefaultUri = Constants.WebAssemblyDefaultUri;
        AppInitializer.TestEnvironment.iOSAppName = Constants.iOSAppName;
        AppInitializer.TestEnvironment.AndroidAppName = Constants.AndroidAppName;
        AppInitializer.TestEnvironment.iOSDeviceNameOrId = Constants.iOSDeviceNameOrId;
        AppInitializer.TestEnvironment.CurrentPlatform = Constants.CurrentPlatform;
        AppInitializer.TestEnvironment.WebAssemblyBrowser = Constants.WebAssemblyBrowser;

        if (Constants.CurrentPlatform != Platform.Browser)
        {
            // Start the app only once, so the tests runs don't restart it
            // and gain some time for the tests.
            AppInitializer.ColdStartApp();
        }
    }

    protected IApp App
    {
        get => _app!;
        private set
        {
            _app = value;
            Uno.UITest.Helpers.Queries.Helpers.App = value;
        }
    }

    [SetUp]
    public void SetUpTest()
    {
        var browserDriverPath = NormalizeBrowserDriverPath();
        if (Constants.CurrentPlatform == Platform.Browser &&
            string.IsNullOrWhiteSpace(browserDriverPath))
        {
            Assert.Ignore(MissingBrowserDriverMessage);
        }

        App = Constants.CurrentPlatform == Platform.Browser
            ? EnsureBrowserApp(browserDriverPath!)
            : AppInitializer.AttachToApp();
    }

    [TearDown]
    public void TearDownTest()
    {
        if (_app is not null)
        {
            TakeScreenshot("teardown");
        }
    }

    [OneTimeTearDown]
    public void TearDownFixture()
    {
        if (_app is not null && !ReferenceEquals(_app, _browserApp))
        {
            _app.Dispose();
        }

        _app = null;

        _browserApp?.Dispose();
        _browserApp = null;

        if (Constants.CurrentPlatform == Platform.Browser)
        {
            BrowserTestHost.Stop();
        }
    }

    public FileInfo TakeScreenshot(string stepName)
    {
        var title = $"{TestContext.CurrentContext.Test.Name}_{stepName}"
            .Replace(" ", "_")
            .Replace(".", "_");

        var fileInfo = App.Screenshot(title);

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
        if (fileNameWithoutExt != title && fileInfo.DirectoryName != null)
        {
            var destFileName = Path
                .Combine(fileInfo.DirectoryName, title + Path.GetExtension(fileInfo.Name));

            if (File.Exists(destFileName))
            {
                File.Delete(destFileName);
            }

            File.Move(fileInfo.FullName, destFileName);

            TestContext.AddTestAttachment(destFileName, stepName);

            fileInfo = new FileInfo(destFileName);
        }
        else
        {
            TestContext.AddTestAttachment(fileInfo.FullName, stepName);
        }

        return fileInfo;
    }

    private static string? NormalizeBrowserDriverPath()
    {
        var configuredPath = Environment.GetEnvironmentVariable(BrowserDriverEnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(configuredPath))
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

        if (Directory.Exists(configuredPath) &&
            File.Exists(Path.Combine(configuredPath, BrowserDriverFileName)))
        {
            return configuredPath;
        }

        return null;
    }

    private static bool ResolveBrowserHeadless()
    {
#if DEBUG
        return !string.Equals(
            Environment.GetEnvironmentVariable(ShowBrowserEnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);
#else
        return true;
#endif
    }

    private static IApp EnsureBrowserApp(string browserDriverPath)
    {
        lock (BrowserAppSyncRoot)
        {
            if (_browserApp is not null)
            {
                return _browserApp;
            }

            var configurator = Uno.UITest.Selenium.ConfigureApp.WebAssembly
                .Uri(new Uri(Constants.WebAssemblyDefaultUri))
                .UsingBrowser(Constants.WebAssemblyBrowser.ToString())
                .DriverPath(browserDriverPath)
                .ScreenShotsPath(AppContext.BaseDirectory)
                .SeleniumArgument($"--window-size={BrowserWindowWidth},{BrowserWindowHeight}")
                .Headless(_browserHeadless);

            if (!_browserHeadless)
            {
                configurator = configurator.SeleniumArgument("--remote-debugging-port=9222");
            }

            _browserApp = configurator.StartApp();
            return _browserApp;
        }
    }

}
