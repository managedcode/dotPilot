using DotPilot.UITests.Harness;
using FluentAssertions;
using OpenQA.Selenium;
using UITestPlatform = Uno.UITest.Helpers.Queries.Platform;

namespace DotPilot.UITests.ChatSessions;

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
    private const string AppSidebarLiveSessionIndicatorAutomationId = "AppSidebarLiveSessionIndicator";
    private const string AppSidebarLiveSessionTitleAutomationId = "AppSidebarLiveSessionTitle";
    private const string ChatNavButtonAutomationId = "ChatNavButton";
    private const string ProvidersNavButtonAutomationId = "ProvidersNavButton";
    private const string AgentsNavButtonAutomationId = "AgentsNavButton";
    private const string ProviderListAutomationId = "ProviderList";
    private const string SelectedProviderTitleAutomationId = "SelectedProviderTitle";
    private const string SettingsSectionMessagesButtonAutomationId = "SettingsSectionMessagesButton";
    private const string CodexProviderEntryAutomationId = "ProviderEntry_Codex";
    private const string AgentCatalogSectionAutomationId = "AgentCatalogSection";
    private const string AgentCatalogListAutomationId = "AgentCatalogList";
    private const string AgentCatalogItemAutomationId = "AgentCatalogItem";
    private const string OpenCreateAgentButtonAutomationId = "OpenCreateAgentButton";
    private const string BuildManuallyButtonAutomationId = "BuildManuallyButton";
    private const string AgentBasicInfoSectionAutomationId = "AgentBasicInfoSection";
    private const string AgentProviderClaudeCodeOptionAutomationId = "AgentProviderOption_ClaudeCode";
    private const string AgentProviderCodexOptionAutomationId = "AgentProviderOption_Codex";
    private const string AgentSelectedProviderTextAutomationId = "AgentSelectedProviderText";
    private const string ChatComposerInputAutomationId = "ChatComposerInput";
    private const string ChatComposerHintAutomationId = "ChatComposerHint";
    private const string ChatComposerSendButtonAutomationId = "ChatComposerSendButton";
    private const string ChatFleetBoardSectionAutomationId = "ChatFleetBoardSection";
    private const string ChatFleetMetricItemAutomationId = "ChatFleetMetricItem";
    private const string ChatFleetSessionItemAutomationId = "ChatFleetSessionItem";
    private const string ChatFleetProviderItemAutomationId = "ChatFleetProviderItem";
    private const string ChatFleetEmptyStateAutomationId = "ChatFleetEmptyState";
    private const string ComposerBehaviorSectionAutomationId = "ComposerBehaviorSection";
    private const string ComposerBehaviorCurrentHintAutomationId = "ComposerBehaviorCurrentHint";
    private const string ComposerBehaviorEnterInsertsNewLineButtonAutomationId = "ComposerBehaviorEnterInsertsNewLineButton";
    private const string ChatStartNewButtonAutomationId = "ChatStartNewButton";
    private const string ChatTitleTextAutomationId = "ChatTitleText";
    private const string ChatMessageTextAutomationId = "ChatMessageText";
    private const string ChatActivityItemAutomationId = "ChatActivityItem";
    private const string ChatActivityLabelAutomationId = "ChatActivityLabel";
    private const string ChatRecentChatItemAutomationId = "ChatRecentChatItem";
    private const string ToggleProviderButtonAutomationId = "ToggleProviderButton";
    private const string SaveAgentButtonAutomationId = "SaveAgentButton";
    private const string DefaultSystemAgentName = "dotPilot System Agent";
    private const string DefaultSessionTitle = "Session with dotPilot System Agent";
    private const string DefaultSystemAgentStartChatButtonAutomationId = "AgentCatalogStartChatButton_dotPilotSystemAgent";
    private const string EditableCodexAgentName = "UI Editable Agent";
    private const string EditedCodexAgentName = "UI Edited Agent";
    private const string EditableCodexAgentDescription = "UI-created editable agent.";
    private const string EditedCodexAgentDescription = "Updated UI-edited agent.";
    private const string EditableCodexAgentEditButtonAutomationId = "AgentCatalogEditButton_UIEditableAgent";
    private const string UserPrompt = "hello from ui";
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
        WaitForElement(ChatActivityItemAutomationId);
        WaitForTextContains(ChatActivityLabelAutomationId, "status", ScreenTransitionTimeout);
        WaitForTextContains(ChatActivityLabelAutomationId, "tool", ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot("chat_default_system_agent_flow");
    }

    [Test]
    public async Task WhenLiveGenerationStartsThenSidebarShowsTheLiveSessionIndicator()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);

        WaitForElement(AppSidebarLiveSessionIndicatorAutomationId);
        WaitForTextContains(AppSidebarLiveSessionTitleAutomationId, "Live session active", ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);
        WaitForAutomationElementToDisappearById(AppSidebarLiveSessionIndicatorAutomationId, ScreenTransitionTimeout);

        TakeScreenshot("sidebar_live_session_indicator");
    }

    [Test]
    public async Task WhenLiveGenerationStartsThenFleetBoardShowsTheActiveSessionAndProviderHealth()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        WaitForElement(ChatFleetBoardSectionAutomationId);
        WaitForTextContains(ChatFleetEmptyStateAutomationId, "No live sessions right now.", ScreenTransitionTimeout);
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);

        WaitForTextContains(ChatFleetMetricItemAutomationId, "Live sessions", ScreenTransitionTimeout);
        WaitForTextContains(ChatFleetSessionItemAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);
        WaitForTextContains(ChatFleetProviderItemAutomationId, "Debug Provider", ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);
        WaitForAutomationElementToDisappearById(AppSidebarLiveSessionIndicatorAutomationId, ScreenTransitionTimeout);
        WaitForAutomationElementToDisappearById(ChatFleetSessionItemAutomationId, ScreenTransitionTimeout);
        WaitForTextContains(ChatFleetEmptyStateAutomationId, "No live sessions right now.", ScreenTransitionTimeout);

        TakeScreenshot("chat_fleet_board_live_session");
    }

    [Test]
    public async Task WhenOpeningAgentsThenCatalogIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(AgentCatalogSectionAutomationId);
        WaitForElement(AgentCatalogListAutomationId);
        WaitForTextContains(AgentCatalogItemAutomationId, DefaultSystemAgentName, ScreenTransitionTimeout);

        TakeScreenshot("agent_catalog");
    }

    [Test]
    public async Task WhenStartingChatFromTheAgentCatalogThenTheChatScreenOpensForThatAgent()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(AgentCatalogSectionAutomationId);
        WaitForTextContains(AgentCatalogItemAutomationId, DefaultSystemAgentName, ScreenTransitionTimeout);

        ClickActionAutomationElement(DefaultSystemAgentStartChatButtonAutomationId, expectElementToDisappear: true);

        WaitForElement(ChatScreenAutomationId);
        WaitForTextContains(ChatPageTitleAutomationId, "Chat", ScreenTransitionTimeout);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        TakeScreenshot("agent_catalog_start_chat_opens_chat");
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
    public async Task WhenSelectingClaudeCodeWhileAuthoringAnAgentThenTheProviderSummaryFollowsTheSelection()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(BuildManuallyButtonAutomationId);
        ClickActionAutomationElement(BuildManuallyButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(AgentBasicInfoSectionAutomationId);
        WaitForTextContains(AgentSelectedProviderTextAutomationId, "Codex", ScreenTransitionTimeout);

        ClickActionAutomationElement(
            AgentProviderClaudeCodeOptionAutomationId,
            () => HasTextContaining(AgentSelectedProviderTextAutomationId, "Claude Code"));

        WaitForTextContains(AgentSelectedProviderTextAutomationId, "Claude Code", ScreenTransitionTimeout);

        TakeScreenshot("agent_builder_provider_selection_follows_combo");
    }

    [Test]
    public async Task WhenSavingANewAgentThenANewChatSessionOpensForThatAgent()
    {
        await Task.CompletedTask;

        EnsureProviderEnabled(CodexProviderEntryAutomationId, "Codex");

        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(BuildManuallyButtonAutomationId);
        ClickActionAutomationElement(BuildManuallyButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(AgentBasicInfoSectionAutomationId);

        ReplaceTextAutomationElement("AgentNameInput", "UI Codex Agent");
        ClickActionAutomationElement(
            AgentProviderCodexOptionAutomationId,
            () => HasTextContaining(AgentSelectedProviderTextAutomationId, "Codex"));
        ReplaceTextAutomationElement("AgentDescriptionInput", "UI-created Codex agent.");
        ReplaceTextAutomationElement("AgentSystemPromptInput", "Answer briefly.");

        ClickActionAutomationElement(SaveAgentButtonAutomationId);

        WaitForElement(ChatScreenAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, "Session with UI Codex Agent", ScreenTransitionTimeout);

        TakeScreenshot("saving_new_agent_opens_chat");
    }

    [Test]
    public async Task WhenEditingASavedAgentThenTheCatalogReflectsTheUpdatedProfile()
    {
        await Task.CompletedTask;

        EnsureProviderEnabled(CodexProviderEntryAutomationId, "Codex");

        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(BuildManuallyButtonAutomationId);
        ClickActionAutomationElement(BuildManuallyButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(AgentBasicInfoSectionAutomationId);

        ReplaceTextAutomationElement("AgentNameInput", EditableCodexAgentName);
        ClickActionAutomationElement(
            AgentProviderCodexOptionAutomationId,
            () => HasTextContaining(AgentSelectedProviderTextAutomationId, "Codex"));
        ReplaceTextAutomationElement("AgentDescriptionInput", EditableCodexAgentDescription);
        ReplaceTextAutomationElement("AgentSystemPromptInput", "Answer briefly.");

        ClickActionAutomationElement(SaveAgentButtonAutomationId);
        WaitForElement(ChatScreenAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, $"Session with {EditableCodexAgentName}", ScreenTransitionTimeout);

        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(AgentCatalogSectionAutomationId);
        ClickActionAutomationElement(EditableCodexAgentEditButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(AgentBasicInfoSectionAutomationId);
        WaitForTextContains(AgentsPageTitleAutomationId, "Edit agent", ScreenTransitionTimeout);

        ReplaceTextAutomationElement("AgentNameInput", EditedCodexAgentName);
        ReplaceTextAutomationElement("AgentDescriptionInput", EditedCodexAgentDescription);
        ReplaceTextAutomationElement("AgentSystemPromptInput", "Answer concisely after edit.");

        ClickActionAutomationElement(SaveAgentButtonAutomationId);

        WaitForElement(AgentCatalogSectionAutomationId);
        WaitForTextContains(AgentsPageTitleAutomationId, "All agents", ScreenTransitionTimeout);
        WaitForTextContains("AgentCatalogStatusMessage", "Saved changes to UI Edited Agent using Codex.", ScreenTransitionTimeout);
        WaitForTextContains(AgentCatalogItemAutomationId, EditedCodexAgentName, ScreenTransitionTimeout);
        WaitForTextContains(AgentCatalogItemAutomationId, EditedCodexAgentDescription, ScreenTransitionTimeout);

        TakeScreenshot("editing_saved_agent_updates_catalog");
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

        TakeScreenshot("chat_message_send_behavior");
    }

    [TestCase(BrowserEnterModifier.Shift)]
    [TestCase(BrowserEnterModifier.Control)]
    [TestCase(BrowserEnterModifier.Alt)]
    [TestCase(BrowserEnterModifier.Command)]
    public async Task WhenEnterSendsThenModifierEnterInsertsANewLine(BrowserEnterModifier modifier)
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressModifierEnterAutomationElement(ChatComposerInputAutomationId, modifier);

        TryReadBrowserInputValue(ChatComposerInputAutomationId, out var composerValue).Should().BeTrue();
        NormalizeText(composerValue).Should().Be(UserPrompt);
        composerValue.Replace("\r\n", "\n", StringComparison.Ordinal).Should().EndWith("\n");
        await Task.Delay(TimeSpan.FromSeconds(1));
        HasTextContaining(ChatMessageTextAutomationId, DebugResponsePrefix).Should().BeFalse();
    }

    [Test]
    public async Task WhenEnterAddsNewLineThenPlainEnterInsertsANewLine()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        ClickActionAutomationElement(SettingsSectionMessagesButtonAutomationId);
        WaitForElement(ComposerBehaviorSectionAutomationId);
        ClickActionAutomationElement(ComposerBehaviorEnterInsertsNewLineButtonAutomationId);
        WaitForTextContains(ComposerBehaviorCurrentHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);

        TapAutomationElement(ChatNavButtonAutomationId);
        EnsureOnChatScreen();
        WaitForTextContains(ChatComposerHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressEnterAutomationElement(ChatComposerInputAutomationId);

        TryReadBrowserInputValue(ChatComposerInputAutomationId, out var composerValue).Should().BeTrue();
        NormalizeText(composerValue).Should().Be(UserPrompt);
        composerValue.Replace("\r\n", "\n", StringComparison.Ordinal).Should().EndWith("\n");
        await Task.Delay(TimeSpan.FromSeconds(1));
        HasTextContaining(ChatMessageTextAutomationId, DebugResponsePrefix).Should().BeFalse();
    }

    [TestCase(BrowserEnterModifier.Shift)]
    [TestCase(BrowserEnterModifier.Control)]
    [TestCase(BrowserEnterModifier.Alt)]
    [TestCase(BrowserEnterModifier.Command)]
    public async Task WhenEnterAddsNewLineThenModifierEnterSendsTheMessage(BrowserEnterModifier modifier)
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        ClickActionAutomationElement(SettingsSectionMessagesButtonAutomationId);
        WaitForElement(ComposerBehaviorSectionAutomationId);
        ClickActionAutomationElement(ComposerBehaviorEnterInsertsNewLineButtonAutomationId);
        WaitForTextContains(ComposerBehaviorCurrentHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);

        TapAutomationElement(ChatNavButtonAutomationId);
        EnsureOnChatScreen();
        WaitForTextContains(ChatComposerHintAutomationId, "Enter adds a new line.", ScreenTransitionTimeout);
        ClickActionAutomationElement(ChatStartNewButtonAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, DefaultSessionTitle, ScreenTransitionTimeout);

        ReplaceTextAutomationElement(ChatComposerInputAutomationId, UserPrompt);
        PressModifierEnterAutomationElement(ChatComposerInputAutomationId, modifier);
        WaitForElement(ChatActivityItemAutomationId);
        WaitForTextContains(ChatActivityLabelAutomationId, "status", ScreenTransitionTimeout);
        WaitForTextContains(ChatActivityLabelAutomationId, "tool", ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot($"chat_{modifier.ToString().ToLowerInvariant()}_enter_send_behavior");
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

    private void EnsureProviderEnabled(string providerEntryAutomationId, string providerDisplayName)
    {
        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForElement(ProviderListAutomationId);
        ClickActionAutomationElement(
            providerEntryAutomationId,
            () => HasTextContaining(SelectedProviderTitleAutomationId, providerDisplayName));
        WaitForElement(ToggleProviderButtonAutomationId);

        var toggleText = ReadPrimaryText(ToggleProviderButtonAutomationId);
        if (toggleText.Contains("Enable provider", StringComparison.Ordinal))
        {
            ClickActionAutomationElement(
                ToggleProviderButtonAutomationId,
                () => HasTextContaining(ToggleProviderButtonAutomationId, "Disable provider"));
            WaitForTextContains(ToggleProviderButtonAutomationId, "Disable provider", ScreenTransitionTimeout);
        }
    }

    private bool HasTextContaining(string automationId, string expectedText)
    {
        var texts = App.Query(automationId)
            .Select(result => NormalizeText(result.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        if (texts.Any(text => text.Contains(expectedText, StringComparison.Ordinal)))
        {
            return true;
        }

        if (TryReadBrowserInputValue(automationId, out var inputValue) &&
            NormalizeText(inputValue).Contains(expectedText, StringComparison.Ordinal))
        {
            return true;
        }

        return TryReadBrowserAutomationTexts(automationId, out var browserTexts) &&
            browserTexts.Any(text => NormalizeText(text).Contains(expectedText, StringComparison.Ordinal));
    }

    private string ReadPrimaryText(string automationId)
    {
        var texts = App.Query(automationId)
            .Select(result => NormalizeText(result.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        if (texts.Length > 0)
        {
            return texts[0];
        }

        return TryReadBrowserInputValue(automationId, out var inputValue)
            ? NormalizeText(inputValue)
            : string.Empty;
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
            WriteBrowserSystemLogs($"wait-timeout:{automationId}");
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
