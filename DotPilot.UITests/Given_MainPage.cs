namespace DotPilot.UITests;

[NonParallelizable]
public class GivenMainPage : TestBase
{
    private static readonly TimeSpan InitialScreenProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ScreenTransitionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QueryRetryFrequency = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ShortProbeTimeout = TimeSpan.FromSeconds(3);
    private const string WorkbenchScreenAutomationId = "WorkbenchScreen";
    private const string SettingsScreenAutomationId = "SettingsScreen";
    private const string AgentBuilderScreenAutomationId = "AgentBuilderScreen";
    private const string WorkbenchSessionTitleAutomationId = "WorkbenchSessionTitle";
    private const string WorkbenchPreviewEditorAutomationId = "WorkbenchPreviewEditor";
    private const string RepositoryNodesListAutomationId = "RepositoryNodesList";
    private const string WorkbenchSearchInputAutomationId = "WorkbenchSearchInput";
    private const string SelectedDocumentTitleAutomationId = "SelectedDocumentTitle";
    private const string DocumentViewModeToggleAutomationId = "DocumentViewModeToggle";
    private const string WorkbenchDiffLinesListAutomationId = "WorkbenchDiffLinesList";
    private const string WorkbenchDiffLineItemAutomationId = "WorkbenchDiffLineItem";
    private const string InspectorModeToggleAutomationId = "InspectorModeToggle";
    private const string ArtifactDockListAutomationId = "ArtifactDockList";
    private const string ArtifactDockItemAutomationId = "ArtifactDockItem";
    private const string RuntimeLogListAutomationId = "RuntimeLogList";
    private const string RuntimeLogItemAutomationId = "RuntimeLogItem";
    private const string WorkbenchNavButtonAutomationId = "WorkbenchNavButton";
    private const string SidebarWorkbenchButtonAutomationId = "SidebarWorkbenchButton";
    private const string SidebarAgentsButtonAutomationId = "SidebarAgentsButton";
    private const string BackToWorkbenchButtonAutomationId = "BackToWorkbenchButton";
    private const string SidebarSettingsButtonAutomationId = "SidebarSettingsButton";
    private const string SettingsCategoryListAutomationId = "SettingsCategoryList";
    private const string SettingsEntriesListAutomationId = "SettingsEntriesList";
    private const string SelectedSettingsCategoryTitleAutomationId = "SelectedSettingsCategoryTitle";
    private const string StorageSettingsCategoryAutomationId = "SettingsCategory-storage";
    private const string SettingsPageRepositoryNodeAutomationId = "RepositoryNode-dotpilot-presentation-settingspage-xaml";
    private const string RuntimeFoundationPanelAutomationId = "RuntimeFoundationPanel";

    [Test]
    public async Task WhenOpeningTheAppThenWorkbenchSectionsAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        EnsureArtifactDockVisible();
        WaitForElement(WorkbenchNavButtonAutomationId);
        WaitForElement(WorkbenchSessionTitleAutomationId);
        WaitForElement(WorkbenchPreviewEditorAutomationId);
        WaitForElement(RepositoryNodesListAutomationId);
        WaitForElement(ArtifactDockListAutomationId);
        WaitForElement(ArtifactDockItemAutomationId);
        WaitForElement(RuntimeFoundationPanelAutomationId);
        TakeScreenshot("workbench_shell_visible");
    }

    [Test]
    public async Task WhenFilteringTheRepositoryThenTheMatchingFileOpens()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        App.ClearText(WorkbenchSearchInputAutomationId);
        App.EnterText(WorkbenchSearchInputAutomationId, "SettingsPage");
        WaitForElement(SettingsPageRepositoryNodeAutomationId);
        App.Tap(SettingsPageRepositoryNodeAutomationId);
        WaitForElement(SelectedDocumentTitleAutomationId);

        var title = GetSingleTextContent(SelectedDocumentTitleAutomationId);
        Assert.That(title, Is.EqualTo("SettingsPage.xaml"));

        TakeScreenshot("repository_search_open_file");
    }

    [Test]
    public async Task WhenSwitchingToDiffReviewThenDiffSurfaceIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        EnsureDiffReviewVisible();
        WaitForElement(WorkbenchDiffLinesListAutomationId);
        WaitForElement(WorkbenchDiffLineItemAutomationId);

        TakeScreenshot("diff_review_visible");
    }

    [Test]
    public async Task WhenSwitchingInspectorModeThenRuntimeLogConsoleIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        EnsureRuntimeLogVisible();
        WaitForElement(RuntimeLogListAutomationId);
        WaitForElement(RuntimeLogItemAutomationId);

        TakeScreenshot("runtime_log_console_visible");
    }

    [Test]
    public async Task WhenNavigatingToSettingsThenCategoriesAndEntriesAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        TapAutomationElement(SidebarSettingsButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForElement(SettingsCategoryListAutomationId);
        WaitForElement(SettingsEntriesListAutomationId);
        App.Tap(StorageSettingsCategoryAutomationId);

        var categoryTitle = GetSingleTextContent(SelectedSettingsCategoryTitleAutomationId);
        Assert.That(categoryTitle, Is.EqualTo("Storage"));

        TakeScreenshot("settings_shell_visible");
    }

    [Test]
    public async Task WhenNavigatingFromSettingsToAgentsThenAgentBuilderIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        TapAutomationElement(SidebarSettingsButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        TapAutomationElement(SidebarAgentsButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(BackToWorkbenchButtonAutomationId);

        TakeScreenshot("settings_to_agents_navigation");
    }

    [Test]
    public async Task WhenNavigatingToSettingsAfterOpeningADocumentThenSettingsScreenIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        App.Tap(SettingsPageRepositoryNodeAutomationId);
        WaitForElement(SelectedDocumentTitleAutomationId);
        TapAutomationElement(SidebarSettingsButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);

        TakeScreenshot("document_to_settings_navigation");
    }

    [Test]
    public async Task WhenNavigatingToAgentsAfterOpeningADocumentThenAgentBuilderIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        App.Tap(SettingsPageRepositoryNodeAutomationId);
        WaitForElement(SelectedDocumentTitleAutomationId);
        TapAutomationElement(SidebarAgentsButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);

        TakeScreenshot("document_to_agents_navigation");
    }

    [Test]
    public async Task WhenNavigatingToSettingsAfterChangingWorkbenchModesThenSettingsScreenIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        App.Tap(SettingsPageRepositoryNodeAutomationId);
        EnsureDiffReviewVisible();
        EnsureRuntimeLogVisible();
        TapAutomationElement(SidebarSettingsButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);

        TakeScreenshot("workbench_modes_to_settings_navigation");
    }

    [Test]
    public async Task WhenRunningAWorkbenchRoundTripThenTheMainShellCanBeRestored()
    {
        await Task.CompletedTask;

        EnsureOnWorkbenchScreen();
        App.Tap(SettingsPageRepositoryNodeAutomationId);
        EnsureDiffReviewVisible();
        EnsureRuntimeLogVisible();
        TapAutomationElement(SidebarSettingsButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        App.Tap(StorageSettingsCategoryAutomationId);
        TapAutomationElement(SidebarAgentsButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        TapAutomationElement(BackToWorkbenchButtonAutomationId);
        EnsureOnWorkbenchScreen();
        WaitForElement(RuntimeFoundationPanelAutomationId);

        TakeScreenshot("workbench_roundtrip_restored");
    }

    private void EnsureOnWorkbenchScreen()
    {
        if (TryWaitForWorkbenchSurface(InitialScreenProbeTimeout))
        {
            return;
        }

        if (TryWaitForElement(SidebarWorkbenchButtonAutomationId, InitialScreenProbeTimeout))
        {
            TapAutomationElement(SidebarWorkbenchButtonAutomationId);
        }
        else if (TryWaitForElement(BackToWorkbenchButtonAutomationId, InitialScreenProbeTimeout))
        {
            TapAutomationElement(BackToWorkbenchButtonAutomationId);
        }

        WaitForElement(WorkbenchScreenAutomationId, "Timed out returning to the workbench screen.", ScreenTransitionTimeout);
        WaitForElement(WorkbenchSearchInputAutomationId);
        WaitForElement(SelectedDocumentTitleAutomationId);
    }

    private bool TryWaitForWorkbenchSurface(TimeSpan timeout)
    {
        if (!TryWaitForElement(WorkbenchScreenAutomationId, timeout))
        {
            return false;
        }

        if (!TryWaitForElement(WorkbenchNavButtonAutomationId, timeout))
        {
            return false;
        }

        if (!TryWaitForElement(WorkbenchSearchInputAutomationId, timeout))
        {
            return false;
        }

        return TryWaitForElement(SelectedDocumentTitleAutomationId, timeout);
    }

    private void EnsureArtifactDockVisible()
    {
        if (TryWaitForElement(ArtifactDockListAutomationId, ShortProbeTimeout))
        {
            return;
        }

        if (TryWaitForElement(RuntimeLogListAutomationId, ShortProbeTimeout))
        {
            App.Tap(InspectorModeToggleAutomationId);
        }

        WaitForElement(ArtifactDockListAutomationId);
    }

    private void EnsureRuntimeLogVisible()
    {
        if (TryWaitForElement(RuntimeLogListAutomationId, ShortProbeTimeout))
        {
            return;
        }

        App.Tap(InspectorModeToggleAutomationId);
        WaitForElement(RuntimeLogListAutomationId);
    }

    private void EnsureDiffReviewVisible()
    {
        if (TryWaitForElement(WorkbenchDiffLinesListAutomationId, ShortProbeTimeout))
        {
            return;
        }

        App.Tap(DocumentViewModeToggleAutomationId);
        WaitForElement(WorkbenchDiffLinesListAutomationId);
    }

    private bool TryWaitForElement(string automationId, TimeSpan timeout)
    {
        try
        {
            WaitForElement(automationId, "Element probe timed out.", timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private string GetSingleTextContent(string automationId)
    {
        var results = App.Query(automationId);
        Assert.That(results, Has.Length.EqualTo(1), $"Expected a single result for automation id '{automationId}'.");
        return NormalizeTextContent(results[0].Text);
    }

    private static string NormalizeTextContent(string value)
    {
        var segments = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', segments);
    }

    private IAppResult[] WaitForElement(string automationId, string? timeoutMessage = null, TimeSpan? timeout = null)
    {
        try
        {
            return App.WaitForElement(
                automationId,
                timeoutMessage ?? $"Timed out waiting for automation id '{automationId}'.",
                timeout ?? ScreenTransitionTimeout,
                QueryRetryFrequency,
                null);
        }
        catch (TimeoutException)
        {
            WriteTimeoutDiagnostics(automationId);
            throw;
        }
    }

    private void WriteTimeoutDiagnostics(string automationId)
    {
        WriteBrowserSystemLogs($"timeout:{automationId}");
        WriteBrowserDomSnapshot($"timeout:{automationId}");
        WriteSelectorDiagnostics(automationId);

        try
        {
            TakeScreenshot($"timeout_{automationId}");
        }
        catch (Exception exception)
        {
            HarnessLog.Write($"Timeout screenshot capture failed for '{automationId}': {exception.Message}");
        }
    }

    private void WriteSelectorDiagnostics(string timedOutAutomationId)
    {
        var automationIds = new[]
        {
            timedOutAutomationId,
            WorkbenchScreenAutomationId,
            SettingsScreenAutomationId,
            AgentBuilderScreenAutomationId,
            WorkbenchNavButtonAutomationId,
            SidebarWorkbenchButtonAutomationId,
            SidebarAgentsButtonAutomationId,
            SidebarSettingsButtonAutomationId,
            WorkbenchSearchInputAutomationId,
            SelectedDocumentTitleAutomationId,
            RuntimeFoundationPanelAutomationId,
            BackToWorkbenchButtonAutomationId,
        };

        foreach (var automationId in automationIds.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var matches = App.Query(automationId);
                HarnessLog.Write($"Selector diagnostic '{automationId}' returned {matches.Length} matches.");
            }
            catch (Exception exception)
            {
                HarnessLog.Write($"Selector diagnostic '{automationId}' failed: {exception.Message}");
            }
        }
    }
}
