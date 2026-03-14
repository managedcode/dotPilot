
namespace DotPilot.UITests.Harness;

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
    private static readonly TimeSpan AppCleanupTimeout = TimeSpan.FromSeconds(15);

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
            HarnessLog.Write($"Browser test target URI is '{Constants.WebAssemblyDefaultUri}'.");
            HarnessLog.Write($"Browser binary path is '{_browserAutomation!.BrowserBinaryPath}'.");
            HarnessLog.Write($"Browser driver directory is '{_browserAutomation.DriverPath}'.");
            HarnessLog.Write("Ensuring browser test host is started.");
            BrowserTestHost.EnsureStarted(Constants.WebAssemblyDefaultUri);
            HarnessLog.Write("Browser test host is reachable.");
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
        HarnessLog.Write($"Starting setup for '{TestContext.CurrentContext.Test.Name}'.");
        App = Constants.CurrentPlatform == Platform.Browser
            ? StartBrowserApp(_browserAutomation!)
            : AppInitializer.AttachToApp();
        HarnessLog.Write($"Setup completed for '{TestContext.CurrentContext.Test.Name}'.");
    }

    [TearDown]
    public void TearDownTest()
    {
        HarnessLog.Write($"Starting teardown for '{TestContext.CurrentContext.Test.Name}'.");
        List<Exception> cleanupFailures = [];

        if (_app is not null)
        {
            TakeScreenshot("teardown");
        }

        if (Constants.CurrentPlatform == Platform.Browser && _app is not null)
        {
            TryCleanup(
                () => _app.Dispose(),
                BrowserAppCleanupOperationName,
                cleanupFailures);
        }

        _app = null;

        if (cleanupFailures.Count == 1)
        {
            HarnessLog.Write("Teardown failed with a single cleanup exception.");
            throw cleanupFailures[0];
        }

        if (cleanupFailures.Count > 1)
        {
            HarnessLog.Write("Teardown failed with multiple cleanup exceptions.");
            throw new AggregateException(cleanupFailures);
        }

        HarnessLog.Write($"Teardown completed for '{TestContext.CurrentContext.Test.Name}'.");
    }

    [OneTimeTearDown]
    public void TearDownFixture()
    {
        HarnessLog.Write("Starting fixture cleanup.");
        List<Exception> cleanupFailures = [];

        if (_app is not null)
        {
            TryCleanup(
                () => _app.Dispose(),
                Constants.CurrentPlatform == Platform.Browser
                    ? BrowserAppCleanupOperationName
                    : AttachedAppCleanupOperationName,
                cleanupFailures);
        }

        _app = null;

        if (Constants.CurrentPlatform == Platform.Browser)
        {
            TryCleanup(
                BrowserTestHost.Stop,
                BrowserHostCleanupOperationName,
                cleanupFailures);
        }

        if (cleanupFailures.Count == 1)
        {
            HarnessLog.Write("Fixture cleanup failed with a single cleanup exception.");
            throw cleanupFailures[0];
        }

        if (cleanupFailures.Count > 1)
        {
            HarnessLog.Write("Fixture cleanup failed with multiple cleanup exceptions.");
            throw new AggregateException(cleanupFailures);
        }

        HarnessLog.Write("Fixture cleanup completed.");
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

    protected void WriteBrowserSystemLogs(string context, int maxEntries = 50)
    {
        if (Constants.CurrentPlatform != Platform.Browser || _app is null)
        {
            return;
        }

        try
        {
            var logEntries = _app.GetSystemLogs()
                .TakeLast(maxEntries)
                .ToArray();

            HarnessLog.Write($"Browser system log dump for '{context}' contains {logEntries.Length} entries.");

            foreach (var entry in logEntries)
            {
                HarnessLog.Write($"BrowserLog {entry.Timestamp:O} {entry.Level}: {entry.Message}");
            }
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser system log dump failed for '{context}': {exception.Message}");
        }
    }

    protected void WriteBrowserDomSnapshot(string context, string? automationId = null)
    {
        if (Constants.CurrentPlatform != Platform.Browser || _app is null)
        {
            return;
        }

        try
        {
            var driver = _app
                .GetType()
                .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(_app);

            if (driver is null)
            {
                HarnessLog.Write($"Browser DOM snapshot skipped for '{context}': Selenium driver field was not found.");
                return;
            }

            var executeScriptMethod = driver.GetType().GetMethod(
                "ExecuteScript",
                [typeof(string), typeof(object[])]);

            if (executeScriptMethod is null)
            {
                HarnessLog.Write($"Browser DOM snapshot skipped for '{context}': ExecuteScript was not found.");
                return;
            }

            static string Normalize(object? value)
            {
                var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                text = text.ReplaceLineEndings(" ");
                return text.Length <= 800 ? text : text[..800];
            }

            object? ExecuteScript(string script)
            {
                return executeScriptMethod.Invoke(driver, [script, Array.Empty<object>()]);
            }

            var readyState = Normalize(ExecuteScript("return document.readyState;"));
            var location = Normalize(ExecuteScript("return window.location.href;"));
            var automationCount = Normalize(ExecuteScript("return document.querySelectorAll('[xamlautomationid]').length;"));
            var automationIds = Normalize(ExecuteScript(
                "return Array.from(document.querySelectorAll('[xamlautomationid]')).slice(0, 25).map(e => e.getAttribute('xamlautomationid')).join(' | ');"));
            var ariaLabels = Normalize(ExecuteScript(
                "return Array.from(document.querySelectorAll('[aria-label]')).slice(0, 25).map(e => e.getAttribute('aria-label')).join(' | ');"));
            var bodyText = Normalize(ExecuteScript("return document.body.innerText;"));
            var bodyHtml = Normalize(ExecuteScript("return document.body.innerHTML;"));
            var inspectedAutomationId = automationId ?? string.Empty;
            var escapedAutomationId = inspectedAutomationId.Replace("'", "\\'", StringComparison.Ordinal);
            var targetHitTest = Normalize(ExecuteScript(string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                escapedAutomationId,
                """
                ';
                    if (!automationId) {
                        return 'no inspected automation id';
                    }

                    const target = document.querySelector(`[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`);
                    if (!target) {
                        return `missing ${automationId}`;
                    }

                    const rect = target.getBoundingClientRect();
                    const x = rect.left + (rect.width / 2);
                    const y = rect.top + (rect.height / 2);
                    const top = document.elementFromPoint(x, y);

                    return JSON.stringify({
                        targetTag: target.tagName,
                        targetClass: target.className,
                        targetId: target.getAttribute('xamlautomationid') ?? '',
                        targetAria: target.getAttribute('aria-label') ?? '',
                        x,
                        y,
                        containsTop: top ? target.contains(top) : false,
                        topTag: top?.tagName ?? '',
                        topClass: top?.className ?? '',
                        topId: top?.getAttribute('xamlautomationid') ?? '',
                        topXamlType: top?.getAttribute('xamltype') ?? '',
                        topAria: top?.getAttribute('aria-label') ?? ''
                    });
                })();
                """)));

            HarnessLog.Write($"Browser DOM snapshot for '{context}': readyState='{readyState}', location='{location}', xamlautomationid-count='{automationCount}'.");
            HarnessLog.Write($"Browser DOM snapshot automation ids for '{context}': {automationIds}");
            HarnessLog.Write($"Browser DOM snapshot aria-labels for '{context}': {ariaLabels}");
            HarnessLog.Write($"Browser DOM snapshot target hit test for '{context}' and automation id '{inspectedAutomationId}': {targetHitTest}");
            HarnessLog.Write($"Browser DOM snapshot innerText for '{context}': {bodyText}");
            HarnessLog.Write($"Browser DOM snapshot innerHTML for '{context}': {bodyHtml}");
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser DOM snapshot failed for '{context}': {exception.Message}");
        }
    }

    protected void TapAutomationElement(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        try
        {
            var matches = App.Query(automationId);
            if (matches.Length > 0)
            {
                var target = matches[0];
                HarnessLog.Write(
                    $"Tap target '{automationId}' enabled='{target.Enabled}' rect='{target.Rect}' text='{target.Text}' label='{target.Label}'.");

                if (Constants.CurrentPlatform == Platform.Browser)
                {
                    App.TapCoordinates(target.Rect.CenterX, target.Rect.CenterY);
                    return;
                }
            }

            App.Tap(automationId);
        }
        catch (InvalidOperationException exception)
        {
            HarnessLog.Write($"Tap failed for '{automationId}': {exception.Message}");

            try
            {
                var matches = App.Query(automationId);
                HarnessLog.Write($"Tap selector '{automationId}' returned {matches.Length} matches.");

                for (var index = 0; index < matches.Length; index++)
                {
                    var match = matches[index];
                    HarnessLog.Write(
                        $"Tap selector '{automationId}' match[{index}] id='{match.Id}' text='{match.Text}' label='{match.Label}' rect='{match.Rect}'.");
                }
            }
            catch (Exception diagnosticException)
            {
                HarnessLog.Write($"Tap selector diagnostics failed for '{automationId}': {diagnosticException.Message}");
            }

            if (Constants.CurrentPlatform == Platform.Browser)
            {
                var fallbackMatches = App.Query(automationId);
                if (fallbackMatches.Length > 0)
                {
                    var fallbackTarget = fallbackMatches[0];
                    HarnessLog.Write(
                        $"Falling back to coordinate tap for '{automationId}' at '{fallbackTarget.Rect.CenterX},{fallbackTarget.Rect.CenterY}'.");
                    App.TapCoordinates(fallbackTarget.Rect.CenterX, fallbackTarget.Rect.CenterY);
                    return;
                }
            }

            WriteBrowserAutomationDiagnostics(automationId);
            WriteBrowserDomSnapshot($"tap:{automationId}", automationId);
            throw;
        }
    }

    protected void ReplaceTextAutomationElement(string automationId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(text);

        if (TrySetBrowserInputValue(automationId, text))
        {
            return;
        }

        App.ClearText(automationId);
        App.EnterText(automationId, text);
    }

    protected void ClickActionAutomationElement(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (TryClickBrowserAutomationElement(automationId))
        {
            return;
        }

        TapAutomationElement(automationId);
    }

    private bool TryClickBrowserAutomationElement(string automationId)
    {
        if (Constants.CurrentPlatform != Platform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            var driver = _app
                .GetType()
                .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(_app);

            if (driver is null)
            {
                return false;
            }

            var executeScriptMethod = driver.GetType().GetMethod(
                "ExecuteScript",
                [typeof(string), typeof(object[])]);

            if (executeScriptMethod is null)
            {
                return false;
            }

            var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
            var outcome = executeScriptMethod.Invoke(
                driver,
                [
                    string.Concat(
                        """
                        return (() => {
                            const automationId = '
                        """,
                        escapedAutomationId,
                        """
                        ';
                            const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                            const host = document.querySelector(selector);
                            if (!host) {
                                return 'missing';
                            }

                            const element = host.matches('button, [role="button"], input[type="button"], input[type="submit"]')
                                ? host
                                : host.querySelector('button, [role="button"], input[type="button"], input[type="submit"]') ?? host;

                            element.scrollIntoView({ block: 'center', inline: 'center' });
                            const rect = element.getBoundingClientRect();
                            const eventInit = {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                view: window,
                                clientX: rect.left + (rect.width / 2),
                                clientY: rect.top + (rect.height / 2),
                                button: 0
                            };

                            element.dispatchEvent(new PointerEvent('pointerover', eventInit));
                            element.dispatchEvent(new PointerEvent('pointerenter', eventInit));
                            element.dispatchEvent(new MouseEvent('mouseover', eventInit));
                            element.dispatchEvent(new MouseEvent('mouseenter', eventInit));
                            element.dispatchEvent(new PointerEvent('pointerdown', eventInit));
                            element.dispatchEvent(new MouseEvent('mousedown', eventInit));
                            element.dispatchEvent(new PointerEvent('pointerup', eventInit));
                            element.dispatchEvent(new MouseEvent('mouseup', eventInit));
                            element.dispatchEvent(new MouseEvent('click', eventInit));
                            return 'clicked';
                        })();
                        """),
                    Array.Empty<object>(),
                ]);

            HarnessLog.Write($"Browser action click outcome for '{automationId}': {outcome}");
            return string.Equals(
                Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture),
                "clicked",
                StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser action click failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private bool TrySetBrowserInputValue(string automationId, string text)
    {
        if (Constants.CurrentPlatform != Platform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            var driver = _app
                .GetType()
                .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(_app);

            if (driver is null)
            {
                return false;
            }

            var executeScriptMethod = driver.GetType().GetMethod(
                "ExecuteScript",
                [typeof(string), typeof(object[])]);

            if (executeScriptMethod is null)
            {
                return false;
            }

            var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
            var escapedText = text
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            var outcome = executeScriptMethod.Invoke(
                driver,
                [
                    string.Concat(
                        """
                        return (() => {
                            const automationId = '
                        """,
                        escapedAutomationId,
                        """
                        ';
                            const value = '
                        """,
                        escapedText,
                        """
                        ';
                            const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                            const host = document.querySelector(selector);
                            if (!host) {
                                return 'missing';
                            }

                            const element = host.matches('input, textarea, [contenteditable="true"]')
                                ? host
                                : host.querySelector('input, textarea, [contenteditable="true"]');
                            if (!element) {
                                return 'not-an-input';
                            }

                            element.scrollIntoView({ block: 'center', inline: 'center' });
                            element.focus();

                            if ('value' in element) {
                                element.value = value;
                            } else {
                                element.textContent = value;
                            }

                            const options = { bubbles: true, cancelable: true, composed: true };
                            element.dispatchEvent(new Event('input', options));
                            element.dispatchEvent(new Event('change', options));
                            element.blur();
                            return 'set';
                        })();
                        """),
                    Array.Empty<object>(),
                ]);

            HarnessLog.Write($"Browser input replacement outcome for '{automationId}': {outcome}");
            return string.Equals(
                Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture),
                "set",
                StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser input replacement failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    protected void WriteBrowserAutomationDiagnostics(string automationId)
    {
        if (Constants.CurrentPlatform != Platform.Browser || _app is null)
        {
            return;
        }

        try
        {
            var driver = _app
                .GetType()
                .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(_app);

            if (driver is null)
            {
                HarnessLog.Write($"Browser automation diagnostics skipped for '{automationId}': Selenium driver field was not found.");
                return;
            }

            var executeScriptMethod = driver.GetType().GetMethod(
                "ExecuteScript",
                [typeof(string), typeof(object[])]);

            if (executeScriptMethod is null)
            {
                HarnessLog.Write($"Browser automation diagnostics skipped for '{automationId}': ExecuteScript was not found.");
                return;
            }

            var script = string.Concat(
                """
                return (() => {
                    const automationId = 
                """,
                "'",
                automationId.Replace("'", "\\'"),
                "'",
                """
                ;
                    const byAutomation = Array.from(document.querySelectorAll(`[xamlautomationid="${automationId}"]`))
                        .map((element, index) => ({
                            index,
                            tag: element.tagName,
                            className: element.className,
                            ariaLabel: element.getAttribute('aria-label') ?? '',
                            xamlAutomationId: element.getAttribute('xamlautomationid') ?? '',
                            xamlType: element.getAttribute('xamltype') ?? '',
                            text: (element.innerText ?? '').trim(),
                            html: element.outerHTML.slice(0, 300)
                        }));
                    const byAria = Array.from(document.querySelectorAll(`[aria-label="${automationId}"]`))
                        .map((element, index) => ({
                            index,
                            tag: element.tagName,
                            className: element.className,
                            ariaLabel: element.getAttribute('aria-label') ?? '',
                            xamlAutomationId: element.getAttribute('xamlautomationid') ?? '',
                            xamlType: element.getAttribute('xamltype') ?? '',
                            text: (element.innerText ?? '').trim(),
                            html: element.outerHTML.slice(0, 300)
                        }));
                    return JSON.stringify({ byAutomation, byAria });
                })();
                """);

            var diagnostics = executeScriptMethod.Invoke(driver, [script, Array.Empty<object>()]);
            HarnessLog.Write($"Browser automation diagnostics for '{automationId}': {diagnostics}");
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser automation diagnostics failed for '{automationId}': {exception.Message}");
        }
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

    private static IApp StartBrowserApp(BrowserAutomationSettings browserAutomation)
    {
        HarnessLog.Write("Starting browser app instance.");
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

        var browserApp = configurator.StartApp();
        HarnessLog.Write("Browser app instance started.");
        return browserApp;
    }

    private static void TryCleanup(Action cleanupAction, string operationName, List<Exception> cleanupFailures)
    {
        try
        {
            HarnessLog.Write($"Running cleanup for '{operationName}'.");
            BoundedCleanup.Run(cleanupAction, AppCleanupTimeout, operationName);
            HarnessLog.Write($"Cleanup completed for '{operationName}'.");
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Cleanup failed for '{operationName}': {exception.Message}");
            cleanupFailures.Add(exception);
        }
    }

}
