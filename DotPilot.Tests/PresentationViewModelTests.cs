using DotPilot.Presentation;

namespace DotPilot.Tests;

public class PresentationViewModelTests
{
    [Test]
    public void MainViewModelExposesChatScreenState()
    {
        var viewModel = new MainViewModel(CreateRuntimeFoundationCatalog());

        viewModel.Title.Should().Be("Design Automation Agent");
        viewModel.StatusSummary.Should().Be("3 members · GPT-4o");
        viewModel.RecentChats.Should().HaveCount(3);
        viewModel.RecentChats.Should().ContainSingle(chat => chat.IsSelected);
        viewModel.Messages.Should().HaveCount(3);
        viewModel.Messages.Should().ContainSingle(message => message.IsCurrentUser);
        viewModel.Members.Should().HaveCount(3);
        viewModel.Agents.Should().ContainSingle(agent => agent.Name == "Design Agent");
        viewModel.RuntimeFoundation.EpicLabel.Should().Be(RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.EmbeddedAgentRuntimeHostEpic));
        viewModel.RuntimeFoundation.Slices.Should().HaveCount(4);
        viewModel.RuntimeFoundation.Providers.Should().Contain(provider => !provider.RequiresExternalToolchain);
    }

    [Test]
    public void SecondViewModelExposesAgentBuilderState()
    {
        var viewModel = new SecondViewModel(CreateRuntimeFoundationCatalog());

        viewModel.PageTitle.Should().Be("Create New Agent");
        viewModel.PageSubtitle.Should().Contain("AI agent");
        viewModel.SystemPrompt.Should().Contain("helpful AI assistant");
        viewModel.TokenSummary.Should().Be("0 / 4,096 tokens");
        viewModel.ExistingAgents.Should().HaveCount(3);
        viewModel.AgentTypes.Should().HaveCount(4);
        viewModel.AgentTypes.Should().ContainSingle(option => option.IsSelected);
        viewModel.AvatarOptions.Should().HaveCount(6);
        viewModel.PromptTemplates.Should().HaveCount(3);
        viewModel.Skills.Should().HaveCount(5);
        viewModel.Skills.Should().Contain(skill => skill.IsEnabled);
        viewModel.Skills.Should().Contain(skill => !skill.IsEnabled);
        viewModel.RuntimeFoundation.DeterministicClientName.Should().Be("In-Repo Test Client");
        viewModel.RuntimeFoundation.Providers.Should().HaveCountGreaterOrEqualTo(4);
    }

    private static RuntimeFoundationCatalog CreateRuntimeFoundationCatalog()
    {
        return new RuntimeFoundationCatalog(new DeterministicAgentRuntimeClient());
    }
}
