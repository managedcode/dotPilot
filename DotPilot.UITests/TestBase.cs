
namespace DotPilot.UITests;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1810:Initialize reference type static fields inline",
    Justification = "UI smoke tests need one-time browser host and driver bootstrap before test execution.")]
public class TestBase
{
    private const string AttachedAppCleanupOperationName = "attached app";
    private const string BrowserAppCleanupOperationName = "browser app";
    private const string BrowserHostCleanupOperationName = "browser host";
    private const string ShowBrowserEnvironmentVariableName = "DOTPILOT_UITEST_SHOW_BROWSER";
    private const string BrowserWindowSizeArgumentPrefix = "--window-size=";
    private const int BrowserWindowWidth = 1440;
    private const int BrowserWindowHeight = 960;
    private static readonly object BrowserAppSyncRoot = new();
    private static readonly TimeSpan AppCleanupTimeout = TimeSpan.FromSeconds(15);

    private static IApp? _browserApp;
    private static readonly BrowserAutomationSettings? _browserAutomation =
        Constants.CurrentPlatform == Platform.Browser
            ? BrowserAutomationBootstrap.Resolve()
            : null;
    private static readonly bool _browserHeadless = ResolveBrowserHeadless();
    private IApp? _app;

    static TestBase()
    {
        if (Constants.CurrentPlatform == Platform.Browser)
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
        App = Constants.CurrentPlatform == Platform.Browser
            ? EnsureBrowserApp(_browserAutomation!)
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
        List<Exception> cleanupFailures = [];

        if (_app is not null && !ReferenceEquals(_app, _browserApp))
        {
            TryCleanup(
                () => _app.Dispose(),
                AttachedAppCleanupOperationName,
                cleanupFailures);
        }

        _app = null;

        try
        {
            if (_browserApp is not null)
            {
                TryCleanup(
                    () => _browserApp.Dispose(),
                    BrowserAppCleanupOperationName,
                    cleanupFailures);
            }
        }
        finally
        {
            _browserApp = null;

            if (Constants.CurrentPlatform == Platform.Browser)
            {
                TryCleanup(
                    BrowserTestHost.Stop,
                    BrowserHostCleanupOperationName,
                    cleanupFailures);
            }
        }

        if (cleanupFailures.Count == 1)
        {
            throw cleanupFailures[0];
        }

        if (cleanupFailures.Count > 1)
        {
            throw new AggregateException(cleanupFailures);
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

    private static IApp EnsureBrowserApp(BrowserAutomationSettings browserAutomation)
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
                .BrowserBinaryPath(browserAutomation.BrowserBinaryPath)
                .ScreenShotsPath(AppContext.BaseDirectory)
                .WindowSize(BrowserWindowWidth, BrowserWindowHeight)
                .SeleniumArgument($"{BrowserWindowSizeArgumentPrefix}{BrowserWindowWidth},{BrowserWindowHeight}")
                .Headless(_browserHeadless);

            configurator = configurator.DriverPath(browserAutomation.DriverPath);

            if (!_browserHeadless)
            {
                configurator = configurator.SeleniumArgument("--remote-debugging-port=9222");
            }

            _browserApp = configurator.StartApp();
            return _browserApp;
        }
    }

    private static void TryCleanup(Action cleanupAction, string operationName, List<Exception> cleanupFailures)
    {
        try
        {
            BoundedCleanup.Run(cleanupAction, AppCleanupTimeout, operationName);
        }
        catch (Exception exception)
        {
            cleanupFailures.Add(exception);
        }
    }

}
