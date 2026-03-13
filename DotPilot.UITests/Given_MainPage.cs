namespace DotPilot.UITests;

public class GivenMainPage : TestBase
{
    private static readonly TimeSpan InitialScreenProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ScreenTransitionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QueryRetryFrequency = TimeSpan.FromMilliseconds(250);
    private const double DesktopSectionMinimumWidth = 900d;
    private const string MainChatScreenAutomationId = "MainChatScreen";
    private const string ChatSidebarAutomationId = "ChatSidebar";
    private const string ChatMessagesListAutomationId = "ChatMessagesList";
    private const string ChatMembersListAutomationId = "ChatMembersList";
    private const string AgentsNavButtonAutomationId = "AgentsNavButton";
    private const string AgentBuilderScreenAutomationId = "AgentBuilderScreen";
    private const string AgentBasicInfoSectionAutomationId = "AgentBasicInfoSection";
    private const string AgentPromptSectionAutomationId = "AgentPromptSection";
    private const string AgentSkillsSectionAutomationId = "AgentSkillsSection";
    private const string PromptTemplateButtonAutomationId = "PromptTemplateButton";
    private const string BackToChatButtonAutomationId = "BackToChatButton";
    private const string SidebarChatButtonAutomationId = "SidebarChatButton";
    private const string RuntimeFoundationPanelAutomationId = "RuntimeFoundationPanel";
    private const string RuntimeFoundationSlicesListAutomationId = "RuntimeFoundationSlicesList";
    private const string RuntimeFoundationProvidersListAutomationId = "RuntimeFoundationProvidersList";
    private const string RuntimeFoundationDeterministicClientAutomationId = "RuntimeFoundationDeterministicClient";
    private const string RuntimeFoundationSliceItemAutomationId = "RuntimeFoundationSliceItem";
    private const string RuntimeFoundationProviderItemAutomationId = "RuntimeFoundationProviderItem";
    private const string AgentBuilderRuntimeBannerAutomationId = "AgentBuilderRuntimeBanner";
    private const string AgentBuilderRuntimeIssueLabelAutomationId = "AgentBuilderRuntimeIssueLabel";
    private const string AgentBuilderRuntimeSummaryAutomationId = "AgentBuilderRuntimeSummary";
    private const string AgentBuilderRuntimeClientAutomationId = "AgentBuilderRuntimeClient";
    private const string AgentBuilderWidthFailureMessage =
        "The browser smoke host dropped below the desktop layout width threshold.";

    [Test]
    public async Task WhenOpeningTheAppThenChatShellSectionsAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.WaitForElement(ByMarked(ChatSidebarAutomationId));
        App.WaitForElement(ByMarked(ChatMessagesListAutomationId));
        App.WaitForElement(ByMarked(ChatMembersListAutomationId));
        App.WaitForElement(ByMarked(RuntimeFoundationPanelAutomationId));
        App.WaitForElement(ByMarked(RuntimeFoundationSlicesListAutomationId));
        App.WaitForElement(ByMarked(RuntimeFoundationProvidersListAutomationId));
        TakeScreenshot("chat_shell_visible");
    }

    [Test]
    public async Task WhenNavigatingToAgentBuilderThenKeySectionsAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.Tap(ByMarked(AgentsNavButtonAutomationId));
        App.WaitForElement(ByMarked(AgentBuilderScreenAutomationId));
        App.WaitForElement(ByMarked(AgentBasicInfoSectionAutomationId));
        App.Find(AgentPromptSectionAutomationId, ScreenTransitionTimeout);
        App.Find(AgentSkillsSectionAutomationId, ScreenTransitionTimeout);
        App.Find(PromptTemplateButtonAutomationId, ScreenTransitionTimeout);
        App.WaitForElement(ByMarked(AgentBuilderRuntimeBannerAutomationId));
        App.WaitForElement(ByMarked(AgentBuilderRuntimeIssueLabelAutomationId));
        App.WaitForElement(ByMarked(AgentBuilderRuntimeSummaryAutomationId));
        App.WaitForElement(ByMarked(AgentBuilderRuntimeClientAutomationId));
        TakeScreenshot("agent_builder_sections_visible");
    }

    [Test]
    public async Task WhenReturningToChatFromAgentBuilderThenChatShellSectionsAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.Tap(ByMarked(AgentsNavButtonAutomationId));
        App.WaitForElement(ByMarked(SidebarChatButtonAutomationId));
        App.Tap(ByMarked(SidebarChatButtonAutomationId));
        App.WaitForElement(ByMarked(MainChatScreenAutomationId));
        App.WaitForElement(ByMarked(ChatMessagesListAutomationId));
        App.WaitForElement(ByMarked(RuntimeFoundationPanelAutomationId));
        TakeScreenshot("chat_shell_restored");
    }

    [Test]
    public async Task WhenOpeningTheAppThenRuntimeFoundationPanelShowsSlicesAndProviders()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.WaitForElement(ByMarked(RuntimeFoundationDeterministicClientAutomationId));

        var sliceItems = GetResults(RuntimeFoundationSliceItemAutomationId);
        var providerItems = GetResults(RuntimeFoundationProviderItemAutomationId);

        Assert.That(sliceItems, Has.Length.EqualTo(4));
        Assert.That(providerItems.Length, Is.GreaterThanOrEqualTo(4));

        TakeScreenshot("runtime_foundation_panel");
    }

    [Test]
    public async Task WhenNavigatingAcrossWorkbenchThenRuntimeFoundationRemainsVisible()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.WaitForElement(ByMarked(RuntimeFoundationPanelAutomationId));
        App.Tap(ByMarked(AgentsNavButtonAutomationId));
        App.WaitForElement(ByMarked(AgentBuilderRuntimeBannerAutomationId));
        App.Tap(ByMarked(SidebarChatButtonAutomationId));
        App.WaitForElement(ByMarked(RuntimeFoundationPanelAutomationId));

        TakeScreenshot("runtime_foundation_roundtrip");
    }

    [Test]
    public async Task WhenOpeningAgentBuilderThenDesktopSectionWidthIsPreserved()
    {
        await Task.CompletedTask;

        EnsureOnMainChatScreen();
        App.Tap(ByMarked(AgentsNavButtonAutomationId));
        App.WaitForElement(ByMarked(AgentBasicInfoSectionAutomationId));

        var basicInfoSection = GetSingleResult(AgentBasicInfoSectionAutomationId);
        Assert.That(
            basicInfoSection.Rect.Width,
            Is.GreaterThanOrEqualTo(DesktopSectionMinimumWidth),
            AgentBuilderWidthFailureMessage);

        TakeScreenshot("agent_builder_desktop_width");
    }

    private void EnsureOnMainChatScreen()
    {
        var mainChatScreen = ByMarked(MainChatScreenAutomationId);
        if (TryWaitForElement(mainChatScreen, InitialScreenProbeTimeout))
        {
            return;
        }

        var backToChatButton = ByMarked(BackToChatButtonAutomationId);
        var sidebarChatButton = ByMarked(SidebarChatButtonAutomationId);
        if (TryWaitForElement(backToChatButton, InitialScreenProbeTimeout))
        {
            App.Tap(backToChatButton);
        }
        else if (TryWaitForElement(sidebarChatButton, InitialScreenProbeTimeout))
        {
            App.Tap(sidebarChatButton);
        }

        App.WaitForElement(
            mainChatScreen,
            timeoutMessage: "Timed out returning to the main chat screen.",
            timeout: ScreenTransitionTimeout,
            retryFrequency: QueryRetryFrequency);
    }

    private bool TryWaitForElement(Query query, TimeSpan timeout)
    {
        try
        {
            App.WaitForElement(
                query,
                timeoutMessage: "Element probe timed out.",
                timeout: timeout,
                retryFrequency: QueryRetryFrequency);

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private IAppResult GetSingleResult(string automationId)
    {
        var results = App.Query(ByMarked(automationId));
        Assert.That(results, Has.Length.EqualTo(1), $"Expected a single result for automation id '{automationId}'.");
        return results[0];
    }

    private IAppResult[] GetResults(string automationId)
    {
        return App.Query(ByMarked(automationId));
    }

    private static Query ByMarked(string automationId)
    {
        return q => q.All().Marked(automationId);
    }
}
