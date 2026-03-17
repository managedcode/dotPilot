using DotPilot.UITests.Harness;
using FluentAssertions;
using OpenQA.Selenium;
using UITestPlatform = Uno.UITest.Helpers.Queries.Platform;

namespace DotPilot.UITests.ChatSessions;

[NonParallelizable]
public sealed class GivenProviderCatalog : TestBase
{
    private static readonly TimeSpan InitialScreenProbeTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QueryRetryFrequency = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ScreenTransitionTimeout = TimeSpan.FromSeconds(60);

    private const string ChatScreenAutomationId = "ChatScreen";
    private const string SettingsScreenAutomationId = "SettingsScreen";
    private const string AgentBuilderScreenAutomationId = "AgentBuilderScreen";
    private const string ChatNavButtonAutomationId = "ChatNavButton";
    private const string ProvidersNavButtonAutomationId = "ProvidersNavButton";
    private const string AgentsNavButtonAutomationId = "AgentsNavButton";
    private const string ProviderListAutomationId = "ProviderList";
    private const string CodexProviderEntryAutomationId = "ProviderEntry_Codex";
    private const string ClaudeProviderEntryAutomationId = "ProviderEntry_ClaudeCode";
    private const string GitHubCopilotProviderEntryAutomationId = "ProviderEntry_GitHubCopilot";
    private const string DebugProviderEntryAutomationId = "ProviderEntry_Debug";
    private const string OpenCreateAgentButtonAutomationId = "OpenCreateAgentButton";
    private const string BuildManuallyButtonAutomationId = "BuildManuallyButton";
    private const string AgentBasicInfoSectionAutomationId = "AgentBasicInfoSection";
    private const string AgentProviderCodexOptionAutomationId = "AgentProviderOption_Codex";
    private const string AgentProviderClaudeCodeOptionAutomationId = "AgentProviderOption_ClaudeCode";
    private const string AgentProviderGitHubCopilotOptionAutomationId = "AgentProviderOption_GitHubCopilot";
    private const string AgentProviderDebugOptionAutomationId = "AgentProviderOption_Debug";

    [Test]
    public void WhenOpeningProvidersThenOnlyThreeRealConsoleProvidersAreVisible()
    {
        EnsureOnChatScreen();
        TapAutomationElement(ProvidersNavButtonAutomationId);
        WaitForElement(SettingsScreenAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(ProviderListAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(CodexProviderEntryAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(ClaudeProviderEntryAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(GitHubCopilotProviderEntryAutomationId, timeout: ScreenTransitionTimeout);

        App.Query(DebugProviderEntryAutomationId).Should().BeEmpty();
        BrowserHasAutomationElement(DebugProviderEntryAutomationId).Should().BeFalse();

        TakeScreenshot("provider_catalog_three_real_providers");
    }

    [Test]
    public void WhenCreatingAnAgentThenOnlyThreeRealProviderQuickActionsAreVisible()
    {
        EnsureOnChatScreen();
        TapAutomationElement(AgentsNavButtonAutomationId);
        WaitForElement(AgentBuilderScreenAutomationId, timeout: ScreenTransitionTimeout);
        ClickActionAutomationElement(OpenCreateAgentButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(BuildManuallyButtonAutomationId, timeout: ScreenTransitionTimeout);
        ClickActionAutomationElement(BuildManuallyButtonAutomationId, expectElementToDisappear: true);
        WaitForElement(AgentBasicInfoSectionAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(AgentProviderCodexOptionAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(AgentProviderClaudeCodeOptionAutomationId, timeout: ScreenTransitionTimeout);
        WaitForElement(AgentProviderGitHubCopilotOptionAutomationId, timeout: ScreenTransitionTimeout);

        App.Query(AgentProviderDebugOptionAutomationId).Should().BeEmpty();
        BrowserHasAutomationElement(AgentProviderDebugOptionAutomationId).Should().BeFalse();

        TakeScreenshot("agent_builder_three_real_provider_actions");
    }

    private void EnsureOnChatScreen()
    {
        if (TryWaitForElement(ChatScreenAutomationId, InitialScreenProbeTimeout))
        {
            return;
        }

        TapAutomationElement(ChatNavButtonAutomationId);
        WaitForElement(ChatScreenAutomationId, timeout: ScreenTransitionTimeout);
    }

    private bool TryWaitForElement(string automationId, TimeSpan timeout)
    {
        try
        {
            WaitForElement(automationId, timeout: timeout);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
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
}
