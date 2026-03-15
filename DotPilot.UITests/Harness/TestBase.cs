using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using UITestPlatform = Uno.UITest.Helpers.Queries.Platform;

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
    private static readonly TimeSpan QueryRetryFrequency = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan AppCleanupTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PostClickTransitionProbeTimeout = TimeSpan.FromSeconds(1);

    private static readonly BrowserAutomationSettings? _browserAutomation =
        Constants.CurrentPlatform == UITestPlatform.Browser
            ? BrowserAutomationBootstrap.Resolve()
            : null;
    private static readonly bool _browserHeadless = ResolveBrowserHeadless();
    private IApp? _app;

    static TestBase()
    {
        if (Constants.CurrentPlatform == UITestPlatform.Browser)
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

        if (Constants.CurrentPlatform != UITestPlatform.Browser)
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
        App = Constants.CurrentPlatform == UITestPlatform.Browser
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

        if (Constants.CurrentPlatform == UITestPlatform.Browser && _app is not null)
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
                Constants.CurrentPlatform == UITestPlatform.Browser
                    ? BrowserAppCleanupOperationName
                    : AttachedAppCleanupOperationName,
                cleanupFailures);
        }

        _app = null;

        if (Constants.CurrentPlatform == UITestPlatform.Browser)
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
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver)
            {
                return;
            }

            var browserLogs = driver.Manage()
                .Logs
                .GetLog("browser")
                .TakeLast(maxEntries)
                .ToArray();

            HarnessLog.Write($"Selenium browser log dump for '{context}' contains {browserLogs.Length} entries.");
            foreach (var entry in browserLogs)
            {
                HarnessLog.Write($"SeleniumBrowserLog {entry.Timestamp:O} {entry.Level}: {entry.Message}");
            }
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Selenium browser log dump failed for '{context}': {exception.Message}");
        }
    }

    protected void WriteBrowserDomSnapshot(string context, string? automationId = null)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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

                if (Constants.CurrentPlatform == UITestPlatform.Browser)
                {
                    TryScrollBrowserAutomationElementIntoView(automationId);

                    try
                    {
                        App.Tap(automationId);
                        HarnessLog.Write($"Uno.UITest tap outcome for '{automationId}': tapped");
                        return;
                    }
                    catch (Exception exception)
                    {
                        HarnessLog.Write($"Uno.UITest tap failed for '{automationId}': {exception.Message}");
                    }

                    if (TryActivateBrowserAutomationElement(automationId))
                    {
                        return;
                    }

                    if (TryClickBrowserAutomationElementAtCenter(automationId))
                    {
                        return;
                    }

                    try
                    {
                        App.TapCoordinates(target.Rect.CenterX, target.Rect.CenterY);
                        HarnessLog.Write($"Coordinate tap outcome for '{automationId}': tapped");
                        return;
                    }
                    catch (Exception exception)
                    {
                        HarnessLog.Write($"Coordinate tap failed for '{automationId}': {exception.Message}");
                    }
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

            if (Constants.CurrentPlatform == UITestPlatform.Browser)
            {
                var fallbackMatches = App.Query(automationId);
                if (fallbackMatches.Length > 0)
                {
                    var fallbackTarget = fallbackMatches[0];
                    TryScrollBrowserAutomationElementIntoView(automationId);
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

        if (TryTypeBrowserInputValue(automationId, text))
        {
            LogAutomationQueryState(automationId, "browser-typed");
            if (TryReadBrowserInputValue(automationId, out var browserValue))
            {
                HarnessLog.Write($"Browser input readback for '{automationId}' after typing: '{browserValue}'.");
                if (string.Equals(browserValue, text, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }

        WriteBrowserAutomationDiagnostics(automationId);

        if (TrySetBrowserInputValue(automationId, text))
        {
            LogAutomationQueryState(automationId, "browser-set");
            if (TryReadBrowserInputValue(automationId, out var browserValue))
            {
                HarnessLog.Write($"Browser input readback for '{automationId}' after replacement: '{browserValue}'.");
            }

            if (DoesAutomationElementReflectText(automationId, text))
            {
                return;
            }

            HarnessLog.Write($"Browser replacement did not update the automation-visible state for '{automationId}'. Falling back to Uno.UITest input.");
        }

        App.ClearText(automationId);
        App.EnterText(automationId, text);
    }

    protected void ClickActionAutomationElement(string automationId, bool expectElementToDisappear = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (Constants.CurrentPlatform == UITestPlatform.Browser)
        {
            TryScrollBrowserAutomationElementIntoView(automationId);

            try
            {
                App.Tap(automationId);
                HarnessLog.Write($"Uno.UITest action tap outcome for '{automationId}': tapped");
                if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                {
                    return;
                }

                HarnessLog.Write($"Action '{automationId}' remained visible after Uno.UITest tap; trying stronger browser fallbacks.");
            }
            catch (Exception exception)
            {
                HarnessLog.Write($"Uno.UITest action tap failed for '{automationId}': {exception.Message}");
            }

            if (TryPerformBrowserClickAction(automationId))
            {
                if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                {
                    return;
                }

                HarnessLog.Write($"Action '{automationId}' remained visible after DOM click; trying the next browser fallback.");
            }

            if (TryClickBrowserAutomationElement(automationId))
            {
                if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                {
                    return;
                }

                HarnessLog.Write($"Action '{automationId}' remained visible after browser element click; trying the next fallback.");
            }

            if (TryClickBrowserAutomationElementAtCenter(automationId))
            {
                if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                {
                    return;
                }

                HarnessLog.Write($"Action '{automationId}' remained visible after center-point click; trying keyboard activation.");
            }

            if (TryActivateBrowserAutomationElement(automationId))
            {
                if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                {
                    return;
                }

                HarnessLog.Write($"Action '{automationId}' remained visible after keyboard activation; trying coordinate tap.");
            }

            try
            {
                var matches = App.Query(automationId);
                if (matches.Length > 0)
                {
                    var target = matches[0];
                    App.TapCoordinates(target.Rect.CenterX, target.Rect.CenterY);
                    HarnessLog.Write($"Coordinate action tap outcome for '{automationId}': tapped");
                    if (!expectElementToDisappear || WaitForAutomationElementToDisappear(automationId))
                    {
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                HarnessLog.Write($"Coordinate action tap failed for '{automationId}': {exception.Message}");
            }
        }

        TapAutomationElement(automationId);
    }

    private bool WaitForAutomationElementToDisappear(string automationId)
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(PostClickTransitionProbeTimeout);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (!BrowserHasAutomationElement(automationId))
            {
                return true;
            }

            Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
        }

        return !BrowserHasAutomationElement(automationId);
    }

    protected void PressEnterAutomationElement(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (TryPressEnterBrowserInput(automationId))
        {
            return;
        }

        App.EnterText(automationId, Keys.Enter);
    }

    protected void SelectComboBoxAutomationElementOption(string automationId, string optionText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionText);

        if (TrySelectBrowserComboBoxOption(automationId, optionText))
        {
            return;
        }

        WriteBrowserAutomationDiagnostics(automationId);
        WriteBrowserDomSnapshot($"select-combo:{automationId}", automationId);
        throw new InvalidOperationException(
            $"Could not select combo-box option '{optionText}' for automation id '{automationId}'.");
    }

    private bool TryActivateBrowserAutomationElement(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver ||
                driver is not IJavaScriptExecutor javaScriptExecutor)
            {
                return false;
            }

            var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
            if (javaScriptExecutor.ExecuteScript(
                    string.Concat(
                        """
                        return (() => {
                            const automationId = '
                        """,
                        escapedAutomationId,
                        """
                        ';
                            const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                            const matches = Array.from(document.querySelectorAll(selector));
                            const visibleMatches = matches.filter(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    rect.right > 0 &&
                                    rect.bottom > 0 &&
                                    rect.left < window.innerWidth &&
                                    rect.top < window.innerHeight;
                            });
                            const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? visibleMatches[0]
                                ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? matches[0];
                            if (!host) {
                                return null;
                            }

                            const clickableSelector = 'button, [role="button"], input[type="button"], input[type="submit"], input[type="checkbox"], input[type="radio"], a[href]';
                            const element = host.matches(clickableSelector)
                                ? host
                                : host.closest(clickableSelector) ?? host.querySelector(clickableSelector) ?? host;

                            element.scrollIntoView({ block: 'center', inline: 'center' });
                            if (!element.hasAttribute('tabindex')) {
                                element.setAttribute('tabindex', '0');
                            }

                            if (typeof host.focus === 'function') {
                                host.focus({ preventScroll: true });
                            }

                            if (typeof element.focus === 'function') {
                                element.focus({ preventScroll: true });
                            }

                            return automationId;
                        })();
                        """)) is null)
            {
                return false;
            }

            static string ReadActiveAutomationId(IJavaScriptExecutor executor)
            {
                return Convert.ToString(
                           executor.ExecuteScript(
                               """
                               const active = document.activeElement;
                               return active?.getAttribute('xamlautomationid')
                                   ?? active?.getAttribute('aria-label')
                                   ?? '';
                               """),
                           System.Globalization.CultureInfo.InvariantCulture)
                       ?? string.Empty;
            }

            var activeAutomationId = ReadActiveAutomationId(javaScriptExecutor);
            for (var index = 0; index < 6 && !string.Equals(activeAutomationId, automationId, StringComparison.Ordinal); index++)
            {
                var activeElement = driver.SwitchTo().ActiveElement();
                activeElement.SendKeys(Keys.Tab);
                activeAutomationId = ReadActiveAutomationId(javaScriptExecutor);
            }

            var focusedElement = driver.SwitchTo().ActiveElement();
            focusedElement.SendKeys(Keys.Space);
            HarnessLog.Write(
                $"Browser keyboard activation outcome for '{automationId}': space via active '{ReadActiveAutomationId(javaScriptExecutor)}'");
            return true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser keyboard activation failed for '{automationId}' with space: {exception.Message}");
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver)
            {
                return false;
            }

            var activeElement = driver.SwitchTo().ActiveElement();
            activeElement.SendKeys(Keys.Enter);
            HarnessLog.Write($"Browser keyboard activation outcome for '{automationId}': enter");
            return true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser keyboard activation failed for '{automationId}' with enter: {exception.Message}");
            return false;
        }
    }
    private bool TryClickBrowserAutomationElement(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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
                            const matches = Array.from(document.querySelectorAll(selector));
                            const visibleMatches = matches.filter(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    rect.right > 0 &&
                                    rect.bottom > 0 &&
                                    rect.left < window.innerWidth &&
                                    rect.top < window.innerHeight;
                            });
                            const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? visibleMatches[0]
                                ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? matches[0];
                            if (!host) {
                                return 'missing';
                            }

                            const clickableSelector = 'button, [role="button"], input[type="button"], input[type="submit"], input[type="checkbox"], input[type="radio"], a[href]';
                            const element = host.matches(clickableSelector)
                                ? host
                                : host.closest(clickableSelector) ?? host.querySelector(clickableSelector) ?? host;
                            element.scrollIntoView({ block: 'center', inline: 'center' });
                            if (typeof host.focus === 'function') {
                                host.focus({ preventScroll: true });
                            }
                            if (typeof element.focus === 'function') {
                                element.focus({ preventScroll: true });
                            }

                            if (typeof element.click === 'function') {
                                element.click();
                                return 'clicked';
                            }

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

    private bool TryClickBrowserAutomationElementAtCenter(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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
                            const matches = Array.from(document.querySelectorAll(selector));
                            const visibleMatches = matches.filter(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    rect.right > 0 &&
                                    rect.bottom > 0 &&
                                    rect.left < window.innerWidth &&
                                    rect.top < window.innerHeight;
                            });
                            const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? visibleMatches[0]
                                ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? matches[0];
                            if (!host) {
                                return 'missing';
                            }

                            host.scrollIntoView({ block: 'center', inline: 'center' });
                            const rect = host.getBoundingClientRect();
                            const clientX = rect.left + (rect.width / 2);
                            const clientY = rect.top + (rect.height / 2);
                            const pointTarget = document.elementFromPoint(clientX, clientY) ?? host;
                            const clickableSelector = 'button, [role="button"], input[type="button"], input[type="submit"], input[type="checkbox"], input[type="radio"], a[href], .uno-button, .uno-buttonbase';
                            const target = pointTarget?.closest?.(clickableSelector)
                                ?? (host.matches(clickableSelector)
                                    ? host
                                    : host.closest(clickableSelector) ?? host.querySelector(clickableSelector))
                                ?? host;
                            const eventBase = {
                                bubbles: true,
                                cancelable: true,
                                composed: true,
                                view: window,
                                clientX,
                                clientY,
                                pointerId: 1,
                                pointerType: 'mouse',
                                isPrimary: true
                            };

                            target.dispatchEvent(new PointerEvent('pointerdown', { ...eventBase, button: 0, buttons: 1, pressure: 0.5 }));
                            target.dispatchEvent(new MouseEvent('mousedown', { ...eventBase, button: 0, buttons: 1 }));
                            target.dispatchEvent(new PointerEvent('pointerup', { ...eventBase, button: 0, buttons: 0, pressure: 0 }));
                            target.dispatchEvent(new MouseEvent('mouseup', { ...eventBase, button: 0, buttons: 0 }));
                            target.dispatchEvent(new MouseEvent('click', { ...eventBase, button: 0, buttons: 0 }));
                            return `clicked:${target.tagName}:${target.getAttribute('xamlautomationid') ?? ''}:${target.getAttribute('aria-label') ?? ''}`;
                        })();
                        """),
                    Array.Empty<object>(),
                ]);

            HarnessLog.Write($"Browser center-point click outcome for '{automationId}': {outcome}");
            return Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture)
                ?.StartsWith("clicked:", StringComparison.Ordinal) is true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser center-point click failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private bool TryPerformBrowserClickAction(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver ||
                driver is not IJavaScriptExecutor javaScriptExecutor)
            {
                return false;
            }

            var selector = string.Concat(
                "[xamlautomationid=\"",
                automationId,
                "\"], [aria-label=\"",
                automationId,
                "\"]");
            var matches = driver.FindElements(By.CssSelector(selector));
            var resolvedElement = matches.FirstOrDefault(element =>
            {
                try
                {
                    if (!element.Displayed)
                    {
                        return false;
                    }

                    var inViewport = javaScriptExecutor.ExecuteScript(
                        """
                        const element = arguments[0];
                        if (!element) {
                            return false;
                        }

                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight;
                        """,
                        element);

                    return inViewport is true;
                }
                catch
                {
                    return false;
                }
            }) ?? matches.FirstOrDefault();

            if (resolvedElement is null)
            {
                return false;
            }

            javaScriptExecutor.ExecuteScript(
                """
                const element = arguments[0];
                if (!element) {
                    return;
                }

                element.scrollIntoView({ block: 'center', inline: 'center' });
                """,
                resolvedElement);

            var directClickOutcome = javaScriptExecutor.ExecuteScript(
                """
                const host = arguments[0];
                if (!host) {
                    return false;
                }

                const clickableSelector = 'button, [role="button"], input[type="button"], input[type="submit"], input[type="checkbox"], input[type="radio"], a[href], .uno-button, .uno-buttonbase';
                const element = host.matches(clickableSelector)
                    ? host
                    : host.closest(clickableSelector) ?? host.querySelector(clickableSelector) ?? host;
                if (typeof element.click !== 'function') {
                    return false;
                }

                element.scrollIntoView({ block: 'center', inline: 'center' });
                element.click();
                return true;
                """,
                resolvedElement);
            if (directClickOutcome is true)
            {
                HarnessLog.Write($"Browser native click outcome for '{automationId}': clicked via DOM click");
                return true;
            }

            try
            {
                if (javaScriptExecutor.ExecuteScript(
                        """
                        const element = arguments[0];
                        if (!element) {
                            return null;
                        }

                        const rect = element.getBoundingClientRect();
                        return [
                            Math.round(rect.left + (rect.width / 2)),
                            Math.round(rect.top + (rect.height / 2))
                        ];
                        """,
                        resolvedElement) is System.Collections.ObjectModel.ReadOnlyCollection<object> pointerTarget &&
                    pointerTarget.Count == 2 &&
                    driver is IActionExecutor actionExecutor)
                {
                    var pointerX = Convert.ToInt32(pointerTarget[0], System.Globalization.CultureInfo.InvariantCulture);
                    var pointerY = Convert.ToInt32(pointerTarget[1], System.Globalization.CultureInfo.InvariantCulture);
                    var pointer = new PointerInputDevice(PointerKind.Mouse);
                    var clickSequence = new ActionSequence(pointer);
                    clickSequence.AddAction(pointer.CreatePointerMove(CoordinateOrigin.Viewport, pointerX, pointerY, TimeSpan.Zero));
                    clickSequence.AddAction(pointer.CreatePointerDown(MouseButton.Left));
                    clickSequence.AddAction(pointer.CreatePointerUp(MouseButton.Left));
                    actionExecutor.PerformActions([clickSequence]);
                }
                else
                {
                    resolvedElement.Click();
                }
            }
            catch
            {
                new Actions(driver)
                    .MoveToElement(resolvedElement)
                    .Click()
                    .Perform();
            }

            HarnessLog.Write($"Browser native click outcome for '{automationId}': clicked");
            return true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser native click failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private bool TrySelectBrowserComboBoxOption(string automationId, string optionText)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver ||
                driver is not IJavaScriptExecutor javaScriptExecutor)
            {
                return false;
            }

            var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
            var escapedOptionText = optionText.Replace("'", "\\'", StringComparison.Ordinal);
            var outcome = Convert.ToString(
                javaScriptExecutor.ExecuteScript(
                    string.Concat(
                        """
                        return (() => {
                            const automationId = '
                        """,
                        escapedAutomationId,
                        """
                        ';
                            const optionText = '
                        """,
                        escapedOptionText,
                        """
                        ';
                            const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                            const matches = Array.from(document.querySelectorAll(selector));
                            const host = matches.find(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 && rect.height > 0;
                            }) ?? matches[0];
                            if (!host) {
                                return 'missing';
                            }

                            host.scrollIntoView({ block: 'center', inline: 'center' });

                            const select = host.matches('select')
                                ? host
                                : host.querySelector('select');
                            if (select) {
                                const option = Array.from(select.options).find(candidate =>
                                    (candidate.textContent ?? '').trim() === optionText ||
                                    (candidate.value ?? '').trim() === optionText);
                                if (!option) {
                                    return 'option-missing';
                                }

                                option.selected = true;
                                select.value = option.value;
                                select.dispatchEvent(new Event('input', { bubbles: true }));
                                select.dispatchEvent(new Event('change', { bubbles: true }));
                                return 'selected';
                            }

                            const combobox = host.matches('[role="combobox"], button, input, div')
                                ? host
                                : host.querySelector('[role="combobox"], button, input, div');
                            if (!combobox) {
                                return 'combobox-missing';
                            }

                            combobox.click();

                            const option = Array.from(document.querySelectorAll('[role="option"], option, li, button, div, p, span'))
                                .filter(candidate => candidate !== host && candidate !== combobox)
                                .find(candidate => {
                                    const rect = candidate.getBoundingClientRect();
                                    const style = window.getComputedStyle(candidate);
                                    const isHidden = style.display === 'none' ||
                                        style.visibility === 'hidden' ||
                                        candidate.getAttribute('aria-hidden') === 'true';

                                    return rect.width > 0 &&
                                        rect.height > 0 &&
                                        rect.right > 0 &&
                                        rect.bottom > 0 &&
                                        rect.left < window.innerWidth &&
                                        rect.top < window.innerHeight &&
                                        !isHidden &&
                                        (candidate.textContent ?? '').trim() === optionText;
                                });
                            if (!option) {
                                return 'option-missing';
                            }

                            option.scrollIntoView({ block: 'center', inline: 'center' });
                            option.click();
                            return 'selected';
                        })();
                        """)),
                System.Globalization.CultureInfo.InvariantCulture);

            HarnessLog.Write(
                $"Browser combo-box selection outcome for '{automationId}' and option '{optionText}': {outcome}");
            if (string.Equals(outcome, "selected", StringComparison.Ordinal))
            {
                return true;
            }

            return TrySelectBrowserComboBoxOptionWithKeyboard(driver, automationId, optionText);
        }
        catch (Exception exception)
        {
            HarnessLog.Write(
                $"Browser combo-box selection failed for '{automationId}' and option '{optionText}': {exception.Message}");
            return false;
        }
    }

    private bool TrySelectBrowserComboBoxOptionWithKeyboard(
        IWebDriver driver,
        string automationId,
        string optionText)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionText);

        static bool MatchesTarget(string[] texts, string optionText)
        {
            return texts.Any(text =>
                text.Contains(optionText, StringComparison.Ordinal) ||
                text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(line => string.Equals(line, optionText, StringComparison.Ordinal)));
        }

        bool SelectionReachedTarget()
        {
            return TryReadBrowserAutomationTexts(automationId, out var texts) && MatchesTarget(texts, optionText);
        }

        void LogCurrentSelection(string context)
        {
            if (!TryReadBrowserAutomationTexts(automationId, out var texts))
            {
                HarnessLog.Write($"Browser combo-box text probe for '{automationId}' during '{context}': <unavailable>");
                return;
            }

            HarnessLog.Write(
                $"Browser combo-box text probe for '{automationId}' during '{context}': {string.Join(" | ", texts)}");
        }

        static void SendKeysToFocusedElement(IWebDriver driver, string keys)
        {
            driver.SwitchTo().ActiveElement().SendKeys(keys);
        }

        try
        {
            var host = TryResolveBrowserInputHost(driver, automationId);
            if (host is null)
            {
                return false;
            }

            if (!TryFocusBrowserInput(driver, automationId, host))
            {
                new Actions(driver)
                    .MoveToElement(host)
                    .Click()
                    .Perform();
            }

            LogCurrentSelection("focus");
            if (SelectionReachedTarget())
            {
                return true;
            }

            var actionSteps = new (string Name, Action<IWebDriver, IWebElement> Execute)[]
            {
                ("home", static (webDriver, _) => SendKeysToFocusedElement(webDriver, Keys.Home)),
                ("space", static (webDriver, _) => SendKeysToFocusedElement(webDriver, Keys.Space)),
                ("arrow-down", static (webDriver, _) => SendKeysToFocusedElement(webDriver, Keys.ArrowDown)),
                ("alt-arrow-down", static (webDriver, element) =>
                {
                    new Actions(webDriver)
                        .MoveToElement(element)
                        .Click()
                        .KeyDown(Keys.Alt)
                        .SendKeys(Keys.ArrowDown)
                        .KeyUp(Keys.Alt)
                        .Perform();
                }),
            };

            foreach (var (name, execute) in actionSteps)
            {
                execute(driver, host);
                Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
                LogCurrentSelection(name);
                if (SelectionReachedTarget())
                {
                    SendKeysToFocusedElement(driver, Keys.Enter);
                    HarnessLog.Write(
                        $"Browser combo-box keyboard fallback selected '{optionText}' for '{automationId}'.");
                    return true;
                }
            }

            for (var index = 0; index < 8; index++)
            {
                SendKeysToFocusedElement(driver, Keys.ArrowDown);
                Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
                LogCurrentSelection($"arrow-loop-{index + 1}");
                if (!SelectionReachedTarget())
                {
                    continue;
                }

                SendKeysToFocusedElement(driver, Keys.Enter);
                HarnessLog.Write(
                    $"Browser combo-box arrow fallback selected '{optionText}' for '{automationId}' after {index + 1} moves.");
                return true;
            }
        }
        catch (Exception exception)
        {
            HarnessLog.Write(
                $"Browser combo-box keyboard fallback failed for '{automationId}' and option '{optionText}': {exception.Message}");
        }

        return false;
    }

    private bool TryScrollBrowserAutomationElementIntoView(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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
                            const matches = Array.from(document.querySelectorAll(selector));
                            const visibleMatches = matches.filter(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    rect.right > 0 &&
                                    rect.bottom > 0 &&
                                    rect.left < window.innerWidth &&
                                    rect.top < window.innerHeight;
                            });
                            const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? visibleMatches[0]
                                ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? matches[0];
                            if (!host) {
                                return 'missing';
                            }

                            host.scrollIntoView({ block: 'center', inline: 'center' });
                            return 'scrolled';
                        })();
                        """),
                    Array.Empty<object>(),
                ]);

            HarnessLog.Write($"Browser scroll outcome for '{automationId}': {outcome}");
            return string.Equals(
                Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture),
                "scrolled",
                StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser scroll failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private bool TryTypeBrowserInputValue(string automationId, string text)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver)
            {
                return false;
            }

            var inputHost = TryResolveBrowserInputHost(driver, automationId);
            try
            {
                if (inputHost is not null && TryFocusBrowserInput(driver, automationId, inputHost))
                {
                    ClearActiveBrowserInput(inputHost);
                    inputHost.SendKeys(text);
                    DispatchBrowserInputEvents(driver, inputHost);
                    CommitBrowserInput(driver, inputHost);

                    if (TryReadBrowserInputValue(automationId, out var hostRoutedValue) &&
                        string.Equals(hostRoutedValue, text, StringComparison.Ordinal))
                    {
                        HarnessLog.Write($"Browser host typing outcome for '{automationId}': '{hostRoutedValue}'.");
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                HarnessLog.Write($"Browser host typing path failed for '{automationId}': {exception.Message}");
            }

            var inputElement = TryResolveBrowserInputElement(driver, automationId);
            if (inputElement is null)
            {
                return false;
            }

            PrepareBrowserInputForTyping(driver, inputElement);
            if (!TryFocusBrowserInput(driver, automationId, inputElement))
            {
                return false;
            }

            ClearActiveBrowserInput(inputElement);
            inputElement.SendKeys(text);
            DispatchBrowserInputEvents(driver, inputElement);
            CommitBrowserInput(driver, inputElement);

            var value = inputElement.GetAttribute("value") ?? inputElement.Text ?? string.Empty;
            HarnessLog.Write($"Browser input typing outcome for '{automationId}': '{value}'.");
            return string.Equals(value, text, StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser input typing failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private static void PrepareBrowserInputForTyping(IWebDriver driver, IWebElement inputElement)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(inputElement);

        if (driver is not IJavaScriptExecutor javaScriptExecutor)
        {
            return;
        }

        javaScriptExecutor.ExecuteScript(
            """
            const element = arguments[0];
            if (!element) {
                return;
            }

            const style = window.getComputedStyle(element);
            const isHidden = style.display === 'none' || style.visibility === 'hidden' || Number.parseFloat(style.opacity || '1') === 0;
            if (!isHidden) {
                return;
            }

            if (!element.dataset.codexOriginalStyle) {
                element.dataset.codexOriginalStyle = element.getAttribute('style') ?? '';
            }

            element.style.display = 'block';
            element.style.visibility = 'visible';
            element.style.opacity = '0.01';
            element.style.position = 'fixed';
            element.style.left = '8px';
            element.style.top = '8px';
            element.style.width = `${Math.max(element.getBoundingClientRect().width, 1)}px`;
            element.style.height = `${Math.max(element.getBoundingClientRect().height, 1)}px`;
            element.style.zIndex = '2147483647';
            element.style.pointerEvents = 'auto';
            """,
            inputElement);
    }

    private bool TryPressEnterBrowserInput(string automationId)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
        {
            return false;
        }

        try
        {
            if (_app
                    .GetType()
                    .GetField("_driver", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.GetValue(_app) is not IWebDriver driver ||
                !TryFocusBrowserInput(driver, automationId, elementOverride: null))
            {
                return false;
            }

            if (driver is not IJavaScriptExecutor javaScriptExecutor)
            {
                return false;
            }

            var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
            var outcome = javaScriptExecutor.ExecuteScript(
                string.Concat(
                    """
                    return (() => {
                        const automationId = '
                    """,
                    escapedAutomationId,
                    """
                    ';
                        const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                        const matches = Array.from(document.querySelectorAll(selector));
                        const visibleMatches = matches.filter(element => {
                            const rect = element.getBoundingClientRect();
                            return rect.width > 0 &&
                                rect.height > 0 &&
                                rect.right > 0 &&
                                rect.bottom > 0 &&
                                rect.left < window.innerWidth &&
                                rect.top < window.innerHeight;
                        });
                        const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                            ?? visibleMatches[0]
                            ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                            ?? matches[0];
                        if (!host) {
                            return 'missing';
                        }

                        const inputSelector = 'textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]';
                        const backingSelector = 'textarea, input:not([type="hidden"])';
                        const candidates = [
                            ...(host.matches(inputSelector) ? [host] : []),
                            ...Array.from(host.querySelectorAll(inputSelector))
                        ];
                        const backingElement = host.matches(backingSelector)
                            ? host
                            : host.querySelector(backingSelector);
                        const visibleCandidates = candidates.filter(element => {
                            const rect = element.getBoundingClientRect();
                            const style = window.getComputedStyle(element);
                            const isHidden = style.display === 'none' || style.visibility === 'hidden';
                            const isDisabled = 'disabled' in element && element.disabled;
                            const isReadOnly = 'readOnly' in element && element.readOnly;
                            const isAriaHidden = element.getAttribute('aria-hidden') === 'true';

                            return rect.width > 0 &&
                                rect.height > 0 &&
                                !isHidden &&
                                !isDisabled &&
                                !isReadOnly &&
                                !isAriaHidden;
                        });
                        const visibleBackingElement = backingElement && visibleCandidates.includes(backingElement)
                            ? backingElement
                            : null;
                        const input = (visibleBackingElement ? [visibleBackingElement] : visibleCandidates.length > 0 ? visibleCandidates : backingElement ? [backingElement, ...candidates] : candidates)
                            .map(candidate => {
                                const rect = candidate.getBoundingClientRect();
                                const isTextArea = candidate.tagName === 'TEXTAREA';
                                const isTextInput = candidate.tagName === 'INPUT';
                                const isRoleTextbox = candidate.getAttribute('role') === 'textbox';
                                const score =
                                    (isTextArea ? 1000000 : 0) +
                                    (isTextInput ? 100000 : 0) +
                                    (isRoleTextbox ? 10000 : 0) +
                                    (rect.width * rect.height);

                                return { candidate, score };
                            })
                            .sort((left, right) => right.score - left.score)[0]?.candidate;
                        if (!input) {
                            return 'missing-input';
                        }

                        host.scrollIntoView({ block: 'center', inline: 'center' });
                        if (typeof host.focus === 'function') {
                            host.focus({ preventScroll: true });
                        }
                        if (typeof input.focus === 'function') {
                            input.focus({ preventScroll: true });
                        }

                        const eventInit = {
                            bubbles: true,
                            cancelable: true,
                            composed: true,
                            key: 'Enter',
                            code: 'Enter',
                            keyCode: 13,
                            which: 13
                        };

                        input.dispatchEvent(new KeyboardEvent('keydown', eventInit));
                        input.dispatchEvent(new KeyboardEvent('keypress', eventInit));
                        input.dispatchEvent(new KeyboardEvent('keyup', eventInit));
                        host.dispatchEvent(new KeyboardEvent('keydown', eventInit));
                        host.dispatchEvent(new KeyboardEvent('keypress', eventInit));
                        host.dispatchEvent(new KeyboardEvent('keyup', eventInit));
                        return 'pressed';
                    })();
                    """));

            HarnessLog.Write($"Browser input enter outcome for '{automationId}': {outcome}.");
            if (!string.Equals(
                    Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture),
                    "pressed",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var inputElement = TryResolveBrowserInputElement(driver, automationId);
            inputElement?.SendKeys(Keys.Enter);
            return true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser input enter failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private static void ClearActiveBrowserInput(IWebElement activeElement)
    {
        ArgumentNullException.ThrowIfNull(activeElement);

        try
        {
            activeElement.Clear();
        }
        catch (InvalidElementStateException)
        {
        }

        var currentValue = activeElement.GetAttribute("value") ?? activeElement.Text ?? string.Empty;
        if (string.IsNullOrEmpty(currentValue))
        {
            return;
        }

        var selectAllModifier = OperatingSystem.IsMacOS() ? Keys.Command : Keys.Control;
        activeElement.SendKeys($"{selectAllModifier}a");
        activeElement.SendKeys(Keys.Backspace);
    }

    private static void DispatchBrowserInputEvents(IWebDriver driver, IWebElement activeElement)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(activeElement);

        if (driver is not IJavaScriptExecutor javaScriptExecutor)
        {
            return;
        }

        javaScriptExecutor.ExecuteScript(
            """
            const element = arguments[0];
            if (!element) {
                return;
            }

            const options = { bubbles: true, cancelable: true, composed: true };
            element.dispatchEvent(new Event('input', options));
            element.dispatchEvent(new Event('change', options));
            """,
            activeElement);
    }

    private static void CommitBrowserInput(IWebDriver driver, IWebElement activeElement)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(activeElement);

        if (driver is IJavaScriptExecutor javaScriptExecutor)
        {
            javaScriptExecutor.ExecuteScript(
                """
                const element = arguments[0];
                if (!element) {
                    return;
                }

                const host = element.closest?.('[xamlautomationid]') ?? element.parentElement ?? element;
                const options = { bubbles: true, cancelable: true, composed: true };

                element.dispatchEvent(new Event('change', options));
                element.dispatchEvent(new FocusEvent('blur', options));

                if (host && host !== element) {
                    host.dispatchEvent(new Event('change', options));
                    host.dispatchEvent(new FocusEvent('blur', options));
                }

                if (document.activeElement === element || document.activeElement === host) {
                    document.body.focus();
                }
                """,
                activeElement);
        }

        try
        {
            activeElement.SendKeys(Keys.Tab);
        }
        catch (Exception)
        {
        }
    }

    private static bool TryFocusBrowserInput(
        IWebDriver driver,
        string automationId,
        IWebElement? elementOverride)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (driver is not IJavaScriptExecutor javaScriptExecutor)
        {
            return false;
        }

        var inputElement = elementOverride ?? TryResolveBrowserInputElement(driver, automationId);
        if (inputElement is null)
        {
            HarnessLog.Write($"Browser input focus outcome for '{automationId}': missing");
            return false;
        }

        var outcome = javaScriptExecutor.ExecuteScript(
            """
            const element = arguments[0];
            if (!element) {
                return 'missing';
            }

            element.scrollIntoView({ block: 'center', inline: 'center' });
            element.focus();

            if ('select' in element) {
                element.select();
            }

            return 'focused';
            """,
            inputElement);

        HarnessLog.Write($"Browser input focus outcome for '{automationId}': {outcome}");
        return string.Equals(
            Convert.ToString(outcome, System.Globalization.CultureInfo.InvariantCulture),
            "focused",
            StringComparison.Ordinal);
    }

    private static IWebElement? TryResolveBrowserInputHost(IWebDriver driver, string automationId)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (driver is not IJavaScriptExecutor javaScriptExecutor)
        {
            return null;
        }

        var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
        return javaScriptExecutor.ExecuteScript(
            string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                escapedAutomationId,
                """
                ';
                    const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                    const matches = Array.from(document.querySelectorAll(selector));
                    const visibleMatches = matches.filter(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight;
                    });

                    return visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? visibleMatches[0]
                        ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? matches[0]
                        ?? null;
                })();
                """)) as IWebElement;
    }

    private static IWebElement? TryResolveBrowserInputElement(IWebDriver driver, string automationId)
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (driver is not IJavaScriptExecutor javaScriptExecutor)
        {
            return null;
        }

        var escapedAutomationId = automationId.Replace("'", "\\'", StringComparison.Ordinal);
        return javaScriptExecutor.ExecuteScript(
            string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                escapedAutomationId,
                """
                ';
                    const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                    const matches = Array.from(document.querySelectorAll(selector));
                    const visibleMatches = matches.filter(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight;
                    });
                    const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? visibleMatches[0]
                        ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? matches[0];
                    if (!host) {
                        return null;
                    }

                    const inputSelector = 'textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]';
                    const candidates = [
                        ...(host.matches(inputSelector) ? [host] : []),
                        ...Array.from(host.querySelectorAll(inputSelector))
                    ];
                    const visibleCandidates = candidates.filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = window.getComputedStyle(element);
                        const isHidden = style.display === 'none' || style.visibility === 'hidden';
                        const isDisabled = 'disabled' in element && element.disabled;
                        const isReadOnly = 'readOnly' in element && element.readOnly;
                        const isAriaHidden = element.getAttribute('aria-hidden') === 'true';

                        return rect.width > 0 &&
                            rect.height > 0 &&
                            !isHidden &&
                            !isDisabled &&
                            !isReadOnly &&
                            !isAriaHidden;
                    });
                    const rankedCandidates = (visibleCandidates.length > 0 ? visibleCandidates : candidates)
                        .map(element => {
                            const rect = element.getBoundingClientRect();
                            const isTextArea = element.tagName === 'TEXTAREA';
                            const isTextInput = element.tagName === 'INPUT';
                            const isRoleTextbox = element.getAttribute('role') === 'textbox';
                            const score =
                                (isTextArea ? 1000000 : 0) +
                                (isTextInput ? 100000 : 0) +
                                (isRoleTextbox ? 10000 : 0) +
                                (rect.width * rect.height);

                            return { element, score };
                        })
                        .sort((left, right) => right.score - left.score);

                    return rankedCandidates[0]?.element ?? null;
                })();
                """)) as IWebElement;
    }

    private bool TrySetBrowserInputValue(string automationId, string text)
    {
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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
                            const matches = Array.from(document.querySelectorAll(selector));
                            const visibleMatches = matches.filter(element => {
                                const rect = element.getBoundingClientRect();
                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    rect.right > 0 &&
                                    rect.bottom > 0 &&
                                    rect.left < window.innerWidth &&
                                    rect.top < window.innerHeight;
                            });
                            const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? visibleMatches[0]
                                ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                                ?? matches[0];
                            if (!host) {
                                return 'missing';
                            }

                            const inputSelector = 'textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]';
                            const backingSelector = 'textarea, input:not([type="hidden"])';
                            const candidates = [
                                ...(host.matches(inputSelector) ? [host] : []),
                                ...Array.from(host.querySelectorAll(inputSelector))
                            ];
                            const backingElement = host.matches(backingSelector)
                                ? host
                                : host.querySelector(backingSelector);
                            const visibleCandidates = candidates.filter(element => {
                                const rect = element.getBoundingClientRect();
                                const style = window.getComputedStyle(element);
                                const isHidden = style.display === 'none' || style.visibility === 'hidden';
                                const isDisabled = 'disabled' in element && element.disabled;
                                const isReadOnly = 'readOnly' in element && element.readOnly;
                                const isAriaHidden = element.getAttribute('aria-hidden') === 'true';

                                return rect.width > 0 &&
                                    rect.height > 0 &&
                                    !isHidden &&
                                    !isDisabled &&
                                    !isReadOnly &&
                                    !isAriaHidden;
                            });
                            const visibleBackingElement = backingElement && visibleCandidates.includes(backingElement)
                                ? backingElement
                                : null;
                            const rankedSource = visibleBackingElement
                                ? [visibleBackingElement]
                                : visibleCandidates.length > 0
                                    ? visibleCandidates
                                    : backingElement
                                        ? [backingElement, ...candidates]
                                        : candidates;
                            const element = rankedSource
                                .map(candidate => {
                                    const rect = candidate.getBoundingClientRect();
                                    const isTextArea = candidate.tagName === 'TEXTAREA';
                                    const isTextInput = candidate.tagName === 'INPUT';
                                    const isRoleTextbox = candidate.getAttribute('role') === 'textbox';
                                    const score =
                                        (isTextArea ? 1000000 : 0) +
                                        (isTextInput ? 100000 : 0) +
                                        (isRoleTextbox ? 10000 : 0) +
                                        (rect.width * rect.height);

                                    return { candidate, score };
                                })
                                .sort((left, right) => right.score - left.score)[0]?.candidate;
                            if (!element) {
                                return 'not-an-input';
                            }

                            host.scrollIntoView({ block: 'center', inline: 'center' });
                            if (typeof host.focus === 'function') {
                                host.focus({ preventScroll: true });
                            }
                            if (typeof element.focus === 'function') {
                                element.focus({ preventScroll: true });
                            }

                            if ('value' in element) {
                                element.value = value;
                            } else {
                                element.textContent = value;
                            }

                            const options = { bubbles: true, cancelable: true, composed: true };
                            const inputEvent = typeof InputEvent === 'function'
                                ? new InputEvent('input', { ...options, data: value, inputType: 'insertText' })
                                : new Event('input', options);
                            element.dispatchEvent(new Event('beforeinput', options));
                            element.dispatchEvent(inputEvent);
                            element.dispatchEvent(new Event('change', options));
                            const hostInputEvent = typeof InputEvent === 'function'
                                ? new InputEvent('input', { ...options, data: value, inputType: 'insertText' })
                                : new Event('input', options);
                            host.dispatchEvent(new Event('beforeinput', options));
                            host.dispatchEvent(hostInputEvent);
                            host.dispatchEvent(new Event('change', options));
                            element.blur();
                            if (typeof host.blur === 'function') {
                                host.blur();
                            }
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
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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
                            value: 'value' in element ? element.value : '',
                            childInputs: Array.from(element.querySelectorAll('input, textarea, [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]')).map((child, childIndex) => ({
                                childIndex,
                                tag: child.tagName,
                                className: child.className,
                                value: 'value' in child ? child.value : child.textContent ?? '',
                                ariaLabel: child.getAttribute('aria-label') ?? '',
                                xamlAutomationId: child.getAttribute('xamlautomationid') ?? '',
                                xamlType: child.getAttribute('xamltype') ?? '',
                                width: child.getBoundingClientRect().width,
                                height: child.getBoundingClientRect().height,
                                display: window.getComputedStyle(child).display,
                                visibility: window.getComputedStyle(child).visibility,
                                disabled: 'disabled' in child ? child.disabled : false,
                                readOnly: 'readOnly' in child ? child.readOnly : false
                            })),
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
                            value: 'value' in element ? element.value : '',
                            childInputs: Array.from(element.querySelectorAll('input, textarea, [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]')).map((child, childIndex) => ({
                                childIndex,
                                tag: child.tagName,
                                className: child.className,
                                value: 'value' in child ? child.value : child.textContent ?? '',
                                ariaLabel: child.getAttribute('aria-label') ?? '',
                                xamlAutomationId: child.getAttribute('xamlautomationid') ?? '',
                                xamlType: child.getAttribute('xamltype') ?? '',
                                width: child.getBoundingClientRect().width,
                                height: child.getBoundingClientRect().height,
                                display: window.getComputedStyle(child).display,
                                visibility: window.getComputedStyle(child).visibility,
                                disabled: 'disabled' in child ? child.disabled : false,
                                readOnly: 'readOnly' in child ? child.readOnly : false
                            })),
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

    protected bool BrowserHasAutomationElement(string automationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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

            var script = string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                automationId.Replace("'", "\\'", StringComparison.Ordinal),
                """
                ';
                    const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                    const matches = Array.from(document.querySelectorAll(selector));
                    return matches.some(element => {
                        const rect = element.getBoundingClientRect();
                        const style = window.getComputedStyle(element);
                        const isHidden = style.display === 'none' ||
                            style.visibility === 'hidden' ||
                            element.getAttribute('aria-hidden') === 'true';

                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight &&
                            !isHidden;
                    });
                })();
                """);

            return executeScriptMethod.Invoke(driver, [script, Array.Empty<object>()]) is true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser automation existence check failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    protected bool TryReadBrowserAutomationTexts(string automationId, out string[] texts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        texts = [];
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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

            var script = string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                automationId.Replace("'", "\\'", StringComparison.Ordinal),
                """
                ';
                    const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                    const matches = Array.from(document.querySelectorAll(selector));
                    const visibleMatches = matches.filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = window.getComputedStyle(element);
                        const isHidden = style.display === 'none' ||
                            style.visibility === 'hidden' ||
                            element.getAttribute('aria-hidden') === 'true';

                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight &&
                            !isHidden;
                    });

                    return visibleMatches
                        .map(element => {
                            const inlineText = (element.innerText ?? '').trim();
                            if (inlineText) {
                                return inlineText;
                            }

                            const nestedInput = element.matches('textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]')
                                ? element
                                : element.querySelector('textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]');
                            if (!nestedInput) {
                                return '';
                            }

                            return 'value' in nestedInput
                                ? (nestedInput.value ?? '').trim()
                                : (nestedInput.textContent ?? '').trim();
                        })
                        .filter(text => text);
                })();
                """);

            if (executeScriptMethod.Invoke(driver, [script, Array.Empty<object>()]) is not System.Collections.ObjectModel.ReadOnlyCollection<object> browserTexts)
            {
                return false;
            }

            texts = browserTexts
                .Select(text => Convert.ToString(text, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToArray();

            return texts.Length > 0;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser automation text read failed for '{automationId}': {exception.Message}");
            texts = [];
            return false;
        }
    }

    protected bool TryReadBrowserInputValue(string automationId, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);

        value = string.Empty;
        if (Constants.CurrentPlatform != UITestPlatform.Browser || _app is null)
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

            var script = string.Concat(
                """
                return (() => {
                    const automationId = '
                """,
                automationId.Replace("'", "\\'", StringComparison.Ordinal),
                """
                ';
                    const selector = `[xamlautomationid="${automationId}"], [aria-label="${automationId}"]`;
                    const matches = Array.from(document.querySelectorAll(selector));
                    const visibleMatches = matches.filter(element => {
                        const rect = element.getBoundingClientRect();
                        return rect.width > 0 &&
                            rect.height > 0 &&
                            rect.right > 0 &&
                            rect.bottom > 0 &&
                            rect.left < window.innerWidth &&
                            rect.top < window.innerHeight;
                    });
                    const host = visibleMatches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? visibleMatches[0]
                        ?? matches.find(element => element.getAttribute('xamlautomationid') === automationId)
                        ?? matches[0];
                    if (!host) {
                        return null;
                    }

                    const inputSelector = 'textarea, input:not([type="hidden"]), [contenteditable="true"], [contenteditable="plaintext-only"], [role="textbox"]';
                    const backingSelector = 'textarea, input:not([type="hidden"])';
                    const candidates = [
                        ...(host.matches(inputSelector) ? [host] : []),
                        ...Array.from(host.querySelectorAll(inputSelector))
                    ];
                    const backingElement = host.matches(backingSelector)
                        ? host
                        : host.querySelector(backingSelector);
                    const visibleCandidates = candidates.filter(element => {
                        const rect = element.getBoundingClientRect();
                        const style = window.getComputedStyle(element);
                        const isHidden = style.display === 'none' || style.visibility === 'hidden';
                        const isDisabled = 'disabled' in element && element.disabled;
                        const isReadOnly = 'readOnly' in element && element.readOnly;
                        const isAriaHidden = element.getAttribute('aria-hidden') === 'true';

                        return rect.width > 0 &&
                            rect.height > 0 &&
                            !isHidden &&
                            !isDisabled &&
                            !isReadOnly &&
                            !isAriaHidden;
                    });
                    const element = backingElement ?? (visibleCandidates.length > 0 ? visibleCandidates : candidates)
                        .map(candidate => {
                            const rect = candidate.getBoundingClientRect();
                            const isTextArea = candidate.tagName === 'TEXTAREA';
                            const isTextInput = candidate.tagName === 'INPUT';
                            const isRoleTextbox = candidate.getAttribute('role') === 'textbox';
                            const score =
                                (isTextArea ? 1000000 : 0) +
                                (isTextInput ? 100000 : 0) +
                                (isRoleTextbox ? 10000 : 0) +
                                (rect.width * rect.height);

                            return { candidate, score };
                        })
                        .sort((left, right) => right.score - left.score)[0]?.candidate;
                    if (!element) {
                        return null;
                    }

                    return 'value' in element ? (element.value ?? '') : (element.textContent ?? '');
                })();
                """);

            var result = executeScriptMethod.Invoke(driver, [script, Array.Empty<object>()]);
            value = Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Browser input read failed for '{automationId}': {exception.Message}");
            value = string.Empty;
            return false;
        }
    }

    private void LogAutomationQueryState(string automationId, string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        try
        {
            var matches = App.Query(automationId);
            HarnessLog.Write($"Automation query state for '{automationId}' during '{context}' returned {matches.Length} matches.");

            for (var index = 0; index < matches.Length; index++)
            {
                var match = matches[index];
                HarnessLog.Write(
                    $"Automation query state for '{automationId}' during '{context}' match[{index}] text='{match.Text}' label='{match.Label}' enabled='{match.Enabled}' rect='{match.Rect}'.");
            }
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Automation query state logging failed for '{automationId}' during '{context}': {exception.Message}");
        }
    }

    private bool DoesAutomationElementReflectText(string automationId, string expectedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(expectedText);

        var normalizedExpectedText = NormalizeWhitespace(expectedText);
        try
        {
            return App.Query(automationId)
                .Any(match =>
                    string.Equals(NormalizeWhitespace(match.Text ?? string.Empty), normalizedExpectedText, StringComparison.Ordinal) ||
                    string.Equals(NormalizeWhitespace(match.Label ?? string.Empty), normalizedExpectedText, StringComparison.Ordinal));
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Automation reflection check failed for '{automationId}': {exception.Message}");
            return false;
        }
    }

    private static string NormalizeWhitespace(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var segments = value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', segments);
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
