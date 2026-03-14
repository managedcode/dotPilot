using DotPilot.UITests.Harness;
using OpenQA.Selenium;

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
    private const string ProvidersNavButtonAutomationId = "ProvidersNavButton";
    private const string SettingsSidebarAgentsButtonAutomationId = "SettingsSidebarAgentsButton";
    private const string SettingsSidebarChatButtonAutomationId = "SettingsSidebarChatButton";
    private const string AgentSidebarChatButtonAutomationId = "AgentSidebarChatButton";
    private const string ProviderListAutomationId = "ProviderList";
    private const string SelectedProviderTitleAutomationId = "SelectedProviderTitle";
    private const string ToggleProviderButtonAutomationId = "ToggleProviderButton";
    private const string AgentNameInputAutomationId = "AgentNameInput";
    private const string AgentModelInputAutomationId = "AgentModelInput";
    private const string AgentCreateStatusMessageAutomationId = "AgentCreateStatusMessage";
    private const string CreateAgentButtonAutomationId = "CreateAgentButton";
    private const string ChatComposerInputAutomationId = "ChatComposerInput";
    private const string ChatComposerSendButtonAutomationId = "ChatComposerSendButton";
    private const string ChatStartNewButtonAutomationId = "ChatStartNewButton";
    private const string ChatTitleTextAutomationId = "ChatTitleText";
    private const string ChatMessageTextAutomationId = "ChatMessageText";
    private const string ChatRecentChatItemAutomationId = "ChatRecentChatItem";
    private const string DebugProviderName = "Debug Provider";
    private const string CreatedAgentName = "Debug Agent UI";
    private const string SessionTitle = "Session with Debug Agent UI";
    private const string UserPrompt = "hello from ui";
    private const string ReadyToCreateDebugAgentText = "Ready to create an agent with Debug Provider.";
    private const string DebugResponsePrefix = "Debug provider received: hello from ui";
    private const string DebugToolFinishedText = "Debug workflow finished.";

    [Test]
    public async Task WhenOpeningTheAppThenChatNavigationAndComposerAreVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        WaitForElement(ChatTitleTextAutomationId);
        WaitForElement(ChatComposerInputAutomationId);
        WaitForElement(ChatStartNewButtonAutomationId);

        TakeScreenshot("chat_shell_visible");
    }

    [Test]
    public async Task WhenEnablingDebugCreatingAgentAndSendingMessageThenStreamedTranscriptIsVisible()
    {
        await Task.CompletedTask;

        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId);
        WaitForElement(ProviderListAutomationId);
        WaitForTextContains(SelectedProviderTitleAutomationId, DebugProviderName, ScreenTransitionTimeout);
        TapAutomationElement(ToggleProviderButtonAutomationId);
        WaitForTextContains(ToggleProviderButtonAutomationId, "Disable provider", ScreenTransitionTimeout);

        TapAutomationElement(SettingsSidebarAgentsButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId);
        WaitForElement(AgentNameInputAutomationId);
        WaitForElement(AgentModelInputAutomationId);
        WaitForTextContains(AgentCreateStatusMessageAutomationId, ReadyToCreateDebugAgentText, ScreenTransitionTimeout);
        ReplaceTextAutomationElement(AgentNameInputAutomationId, CreatedAgentName);
        TapAutomationElement(CreateAgentButtonAutomationId);
        WaitForTextContains(AgentCreateStatusMessageAutomationId, "Created Debug Agent UI using Debug Provider.", ScreenTransitionTimeout);

        TapAutomationElement(AgentSidebarChatButtonAutomationId);
        EnsureOnChatScreen();
        TapAutomationElement(ChatStartNewButtonAutomationId);
        WaitForElement(ChatRecentChatItemAutomationId);
        WaitForTextContains(ChatTitleTextAutomationId, SessionTitle, ScreenTransitionTimeout);

        App.EnterText(ChatComposerInputAutomationId, UserPrompt);
        TapAutomationElement(ChatComposerSendButtonAutomationId);
        WaitForTextContains(ChatMessageTextAutomationId, DebugResponsePrefix, ScreenTransitionTimeout);
        WaitForTextContains(ChatMessageTextAutomationId, DebugToolFinishedText, ScreenTransitionTimeout);

        TakeScreenshot("chat_debug_session_flow");
    }

    private void EnsureOnChatScreen()
    {
        if (TryWaitForElement(ChatScreenAutomationId, InitialScreenProbeTimeout))
        {
            return;
        }

        if (TryWaitForElement(AgentSidebarChatButtonAutomationId, InitialScreenProbeTimeout))
        {
            TapAutomationElement(AgentSidebarChatButtonAutomationId);
        }
        else if (TryWaitForElement(SettingsSidebarChatButtonAutomationId, InitialScreenProbeTimeout))
        {
            TapAutomationElement(SettingsSidebarChatButtonAutomationId);
        }

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

            Task.Delay(QueryRetryFrequency).GetAwaiter().GetResult();
        }

        WriteBrowserSystemLogs($"text-timeout:{automationId}");
        WriteBrowserDomSnapshot($"text-timeout:{automationId}", automationId);
        throw new TimeoutException($"Timed out waiting for text '{expectedText}' in automation id '{automationId}'.");
    }

    private IAppResult[] WaitForElement(string automationId, string? timeoutMessage = null, TimeSpan? timeout = null)
    {
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
}
