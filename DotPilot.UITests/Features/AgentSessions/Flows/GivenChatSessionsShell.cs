using DotPilot.UITests.Harness;
using OpenQA.Selenium;
using UITestPlatform = Uno.UITest.Helpers.Queries.Platform;

namespace DotPilot.UITests.Features.AgentSessions;

[NonParallelizable]
public sealed class GivenChatSessionsShell : TestBase
{
    private static readonly TimeSpan InitialScreenProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ScreenTransitionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QueryRetryFrequency = TimeSpan.FromMilliseconds(250);

    private const string ChatScreenAutomationId = "ChatScreen";
    private const string SettingsScreenAutomationId = "SettingsScreen";
    private const string AgentBuilderScreenAutomationId = "AgentBuilderScreen";
    private const string ChatPageTitleAutomationId = "ChatPageTitle";
    private const string AgentsPageTitleAutomationId = "AgentsPageTitle";
    private const string ProvidersPageTitleAutomationId = "ProvidersPageTitle";
    private const string AppSidebarAutomationId = "AppSidebar";
    private const string AppSidebarBrandAutomationId = "AppSidebarBrand";
    private const string AppSidebarNavigationAutomationId = "AppSidebarNavigation";
    private const string AppSidebarProfileAutomationId = "AppSidebarProfile";
    private const string ChatNavButtonAutomationId = "ChatNavButton";
    private const string ProvidersNavButtonAutomationId = "ProvidersNavButton";
    private const string AgentsNavButtonAutomationId = "AgentsNavButton";
    private const string ProviderListAutomationId = "ProviderList";
    private const string SelectedProviderTitleAutomationId = "SelectedProviderTitle";
    private const string ToggleProviderButtonAutomationId = "ToggleProviderButton";
    private const string SettingsSectionMessagesButtonAutomationId = "SettingsSectionMessagesButton";
    private const string CodexProviderEntryAutomationId = "ProviderEntry_Codex";
    private const string AgentCatalogSectionAutomationId = "AgentCatalogSection";
    private const string AgentCatalogListAutomationId = "AgentCatalogList";
    private const string AgentCatalogItemAutomationId = "AgentCatalogItem";
    private const string AgentCatalogStatusMessageAutomationId = "AgentCatalogStatusMessage";
    private const string OpenCreateAgentButtonAutomationId = "OpenCreateAgentButton";
    private const string AgentPromptInputAutomationId = "AgentPromptInput";
    private const string GenerateAgentButtonAutomationId = "GenerateAgentButton";
    private const string AgentBackButtonAutomationId = "AgentBackButton";
    private const string AgentNameInputAutomationId = "AgentNameInput";
    private const string AgentModelInputAutomationId = "AgentModelInput";
    private const string AgentDescriptionInputAutomationId = "AgentDescriptionInput";
    private const string AgentSystemPromptInputAutomationId = "AgentSystemPromptInput";
    private const string AgentToolsListAutomationId = "AgentToolsList";
    private const string AgentSkillListAutomationId = "AgentSkillList";
    private const string AgentRoleComboAutomationId = "AgentRoleCombo";
    private const string SaveAgentButtonAutomationId = "SaveAgentButton";
    private const string AgentEditorStatusMessageAutomationId = "AgentEditorStatusMessage";
    private const string ChatComposerInputAutomationId = "ChatComposerInput";
    private const string ChatComposerHintAutomationId = "ChatComposerHint";
    private const string ChatComposerSendButtonAutomationId = "ChatComposerSendButton";
    private const string ComposerBehaviorSectionAutomationId = "ComposerBehaviorSection";
    private const string ComposerBehaviorCurrentHintAutomationId = "ComposerBehaviorCurrentHint";
    private const string ComposerBehaviorEnterInsertsNewLineButtonAutomationId = "ComposerBehaviorEnterInsertsNewLineButton";
    private const string ChatStartNewButtonAutomationId = "ChatStartNewButton";
    private const string ChatTitleTextAutomationId = "ChatTitleText";
    private const string ChatMessageTextAutomationId = "ChatMessageText";
    private const string ChatRecentChatItemAutomationId = "ChatRecentChatItem";
    private const string AgentCatalogStartChatButtonAutomationId = "AgentCatalogStartChatButton";
    private const string DebugProviderName = "Debug Provider";
    private const string RepositoryReviewerPrompt = "Create a repository reviewer";
    private const string RepositoryReviewerName = "Repository Reviewer Agent";
    private const string RepositoryReviewerSessionTitle = "Session with Repository Reviewer Agent";
    private const string DefaultSystemAgentName = "dotPilot System Agent";
    private const string DefaultSessionTitle = "Session with dotPilot System Agent";
    private const string UserPrompt = "hello from ui";
    private const string SavedAgentMessage = "Saved Repository Reviewer Agent using Debug Provider.";
    private const string DebugResponsePrefix = "Debug provider received: hello from ui";
    private const string DebugToolFinishedText = "Debug workflow finished.";

    [Test]
    public async Task WhenSwitchingBetweenPrimaryScreensThenOneStableShellChromeRemainsVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        WaitForTextContains(ChatPageTitleAutomationId, "Chat", ScreenTransitionTimeout);
        AssertSingleShellChrome();

        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForTextContains(AgentsPageTitleAutomationId, "All agents", ScreenTransitionTimeout);
        AssertSingleShellChrome();

        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForTextContains(ProvidersPageTitleAutomationId, "Settings", ScreenTransitionTimeout);
        AssertSingleShellChrome();

        TapAutomationElement(ChatNavButtonAutomationId);
        WaitForElement(ChatScreenAutomationId);
        WaitForTextContains(ChatPageTitleAutomationId, "Chat", ScreenTransitionTimeout);
        AssertSingleShellChrome();

        TakeScreenshot("stable_shell_chrome");
    }

    [Test]
    public async Task WhenOpeningTheAppThenDefaultSystemAgentCanStartAChat()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        WaitForElement(ChatTitleTextAutomationId);
        WaitForElement(ChatComposerInputAutomationId);
        WaitForElement(ChatComposerSendButtonAutomationId);
        WaitForTextContains(ChatComposerHintAutomationId, "Enter sends.", ScreenTransitionTimeout);
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForElement(ChatRecentChatItemAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot("chat_default_system_agent_flow");
    }

    [Test]
    public async Task WhenOpeningAgentsThenCatalogAndPromptFirstEntryAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(AgentCatalogSectionAutomationId);
        WaitForElement(AgentCatalogListAutomationId);
        WaitForTextContains(AgentCatalogItemAutomationId, DefaultSystemAgentName, ScreenTransitionTimeout);

        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId);
        WaitForElement(AgentPromptInputAutomationId);
        WaitForElement(GenerateAgentButtonAutomationId);
        WaitForElement(AgentBackButtonAutomationId);

        TakeScreenshot("agent_prompt_first_entry");
    }

    [Test]
    public async Task WhenGeneratingAndSavingAnAgentThenCatalogShowsTheCreatedProfile()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId);
        WaitForElement(AgentPromptInputAutomationId);
        ReplaceTextAutomationElement(AgentPromptInputAutomationId, RepositoryReviewerPrompt);
        ClickActionAutomationElement(GenerateAgentButtonAutomationId);
        WaitForTextContains(AgentEditorStatusMessageAutomationId, "Generated draft", ScreenTransitionTimeout);
        WaitForElement(AgentNameInputAutomationId);
        WaitForElement(AgentModelInputAutomationId);
        WaitForElement(AgentDescriptionInputAutomationId);
        WaitForElement(AgentSystemPromptInputAutomationId);
        WaitForElement(AgentToolsListAutomationId);
        WaitForElement(AgentSkillListAutomationId);
        Assert.That(App.Query(AgentRoleComboAutomationId), Is.Empty);

        ClickActionAutomationElement(SaveAgentButtonAutomationId);
        WaitForElement(AgentCatalogSectionAutomationId);
        WaitForTextContains(AgentCatalogStatusMessageAutomationId, SavedAgentMessage, ScreenTransitionTimeout);
        WaitForTextContains(AgentCatalogItemAutomationId, RepositoryReviewerName, ScreenTransitionTimeout);

        TakeScreenshot("agent_catalog_saved_profile");
    }

    [Test]
    public async Task WhenTogglingTheDebugProviderThenTheSettingsProjectionUpdates()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForTextContains(ProvidersPageTitleAutomationId, "Settings", ScreenTransitionTimeout);
        WaitForElement(ProviderListAutomationId);
        WaitForTextContains(SelectedProviderTitleAutomationId, DebugProviderName, ScreenTransitionTimeout);
        WaitForTextContains(ToggleProviderButtonAutomationId, "Disable provider", ScreenTransitionTimeout);

        ClickActionAutomationElement(ToggleProviderButtonAutomationId);
        WaitForTextContains(ToggleProviderButtonAutomationId, "Enable provider", ScreenTransitionTimeout);
        ClickActionAutomationElement(ToggleProviderButtonAutomationId);
        WaitForTextContains(ToggleProviderButtonAutomationId, "Disable provider", ScreenTransitionTimeout);

        TakeScreenshot("settings_toggle_debug_provider");
    }

    [Test]
    public async Task WhenSelectingCodexFromProvidersThenDetailsUpdateAndAgentsNavigationStillWorks()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForElement(ProviderListAutomationId);

        TapAutomationElement(CodexProviderEntryAutomationId);
        WaitForTextContains(SelectedProviderTitleAutomationId, "Codex", ScreenTransitionTimeout);

        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);

        TakeScreenshot("settings_select_provider_then_agents_navigation");
    }

    [Test]
    public async Task WhenChangingMessageSendBehaviorThenChatHintReflectsTheSelection()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForTextContains(ProvidersPageTitleAutomationId, "Settings", ScreenTransitionTimeout);
        ClickActionAutomationElement(SettingsSectionMessagesButtonAutomationId);
        WaitForElement(ComposerBehaviorSectionAutomationId);
        WaitForTextContains(ComposerBehaviorCurrentHintAutomationId, "Enter sends.", ScreenTransitionTimeout);

        ClickActionAutomationElement(ComposerBehaviorEnterInsertsNewLineButtonAutomationId);
        WaitForTextContains(ComposerBehaviorCurrentHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);

        TapAutomationElement(ChatNavButtonAutomationId);
        EnsureOnChatScreen();
        WaitForTextContains(ChatComposerHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);
        WaitForTextContains(ChatComposerInputAutomationId, UserPrompt, ScreenTransitionTimeout);
        ClickActionAutomationElement(ChatComposerSendButtonAutomationId);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot("chat_message_send_behavior");
    }

    [Test]
    public async Task WhenCreatingAnAgentAndStartingAChatThenTheTranscriptIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId);
        WaitForElement(AgentPromptInputAutomationId);
        ReplaceTextAutomationElement(AgentPromptInputAutomationId, RepositoryReviewerPrompt);
        ClickActionAutomationElement(GenerateAgentButtonAutomationId);
        WaitForElement(AgentNameInputAutomationId);
        ClickActionAutomationElement(SaveAgentButtonAutomationId);
        WaitForTextContains(AgentCatalogStatusMessageAutomationId, SavedAgentMessage, ScreenTransitionTimeout);
        TapAutomationElement(AgentCatalogStartChatButtonAutomationId);

        TapAutomationElement(ChatNavButtonAutomationId);
        EnsureOnChatScreen();
        WaitForElement(ChatRecentChatItemAutomationId);
        WaitForElement(ChatComposerSendButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, RepositoryReviewerSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot("chat_created_agent_flow");
    }

    private void EnsureOnChatScreen()
    {
        if (TryWaitForElement(ChatScreenAutomationId, InitialScreenProbeTimeout))
        {
            return;
        }

        TapAutomationElement(ChatNavButtonAutomationId);

        WaitForElement(ChatScreenAutomationId, "Timed out returning to the chat screen.", ScreenTransitionTimeout);
        WaitForElement(ChatComposerInputAutomationId);
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

    private void WaitForTextContains(string automationId, string expectedText, TimeSpan timeout)
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            string[] texts;
            try
            {
                texts = App.Query(automationId)
                    .Select(result => NormalizeText(result.Text))
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToArray();
            }
            catch (StaleElementReferenceException)
            {
                Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
                continue;
            }
            catch (InvalidOperationException)
            {
                Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
                continue;
            }

            if (texts.Any(text => text.Contains(expectedText, StringComparison.Ordinal)))
            {
                return;
            }

            if (TryReadBrowserInputValue(automationId, out var inputValue) &&
                NormalizeText(inputValue).Contains(expectedText, StringComparison.Ordinal))
            {
                return;
            }

            if (TryReadBrowserAutomationTexts(automationId, out var browserTexts) &&
                browserTexts.Any(text => NormalizeText(text).Contains(expectedText, StringComparison.Ordinal)))
            {
                return;
            }

            Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
        }

        WriteBrowserSystemLogs($"text-timeout:{automationId}");
        WriteBrowserDomSnapshot($"text-timeout:{automationId}", automationId);
        throw new TimeoutException($"Timed out waiting for text '{expectedText}' in automation id '{automationId}'.");
    }

    private IAppResult[] WaitForElement(string automationId, string? timeoutMessage = null, TimeSpan? timeout = null)
    {
        if (Constants.CurrentPlatform == UITestPlatform.Browser)
        {
            var effectiveTimeout = timeout ?? ScreenTransitionTimeout;
            var timeoutAt = DateTimeOffset.UtcNow.Add(effectiveTimeout);

            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                try
                {
                    var matches = App.Query(automationId);
                    if (matches.Length > 0)
                    {
                        return matches;
                    }
                }
                catch (StaleElementReferenceException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                if (BrowserHasAutomationElement(automationId))
                {
                    return [];
                }

                Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
            }

            WriteBrowserAutomationDiagnostics(automationId);
            WriteBrowserDomSnapshot($"wait-timeout:{automationId}", automationId);
            throw new TimeoutException(timeoutMessage ?? $"Timed out waiting for automation id '{automationId}'.");
        }

        return App.WaitForElement(
            automationId,
            timeoutMessage ?? $"Timed out waiting for automation id '{automationId}'.",
            timeout ?? ScreenTransitionTimeout,
            QueryRetryFrequency,
            null);
    }

    private static string NormalizeText(string value)
    {
        var segments = value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', segments);
    }

    private void AssertSingleShellChrome()
    {
        WaitForElement(AppSidebarAutomationId);
        WaitForElement(AppSidebarBrandAutomationId);
        WaitForElement(AppSidebarNavigationAutomationId);
        WaitForElement(AppSidebarProfileAutomationId);

        Assert.Multiple(() =>
        {
            Assert.That(App.Query(AppSidebarAutomationId), Has.Length.EqualTo(1));
            Assert.That(App.Query(AppSidebarBrandAutomationId), Has.Length.EqualTo(1));
            Assert.That(App.Query(AppSidebarNavigationAutomationId), Has.Length.EqualTo(1));
            Assert.That(App.Query(AppSidebarProfileAutomationId), Has.Length.EqualTo(1));
        });
    }
}
