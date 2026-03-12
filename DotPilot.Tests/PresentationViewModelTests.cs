using DotPilot.Presentation;

namespace DotPilot.Tests;

public class PresentationViewModelTests
{
    [Test]
    public void MainViewModel_ExposesChatScreenState()
    {
        var viewModel = new MainViewModel();

        viewModel.Title.Should().Be("Design Automation Agent");
        viewModel.StatusSummary.Should().Be("3 members · GPT-4o");
        viewModel.RecentChats.Should().HaveCount(3);
        viewModel.RecentChats.Should().ContainSingle(chat => chat.IsSelected);
        viewModel.Messages.Should().HaveCount(3);
        viewModel.Messages.Should().ContainSingle(message => message.IsCurrentUser);
        viewModel.Members.Should().HaveCount(3);
        viewModel.Agents.Should().ContainSingle(agent => agent.Name == "Design Agent");
    }

    [Test]
    public void SecondViewModel_ExposesAgentBuilderState()
    {
        var viewModel = new SecondViewModel();

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
    }
}
