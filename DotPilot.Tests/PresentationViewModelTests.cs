using DotPilot.Presentation;
using DotPilot.Runtime.Features.Workbench;

namespace DotPilot.Tests;

public class PresentationViewModelTests
{
    [Test]
    public void MainViewModelExposesWorkbenchShellState()
    {
        using var workspace = TemporaryWorkbenchDirectory.Create();
        var runtimeFoundationCatalog = CreateRuntimeFoundationCatalog();
        var viewModel = new MainViewModel(
            new WorkbenchCatalog(runtimeFoundationCatalog, workspace.Root),
            runtimeFoundationCatalog);

        viewModel.EpicLabel.Should().Be(WorkbenchIssues.FormatIssueLabel(WorkbenchIssues.DesktopWorkbenchEpic));
        viewModel.WorkspaceRoot.Should().Be(workspace.Root);
        viewModel.FilteredRepositoryNodes.Should().NotBeEmpty();
        viewModel.SelectedDocumentTitle.Should().NotBeEmpty();
        viewModel.IsPreviewMode.Should().BeTrue();
        viewModel.RepositorySearchText = "SettingsPage";
        viewModel.FilteredRepositoryNodes.Should().ContainSingle(node => node.RelativePath == "src/SettingsPage.xaml");
        viewModel.SelectedDocumentTitle.Should().Be("SettingsPage.xaml");
        viewModel.IsDiffReviewMode = true;
        viewModel.IsPreviewMode.Should().BeFalse();
        viewModel.IsLogConsoleVisible = true;
        viewModel.IsArtifactsVisible.Should().BeFalse();
        viewModel.RuntimeFoundation.EpicLabel.Should().Be(RuntimeFoundationIssues.FormatIssueLabel(RuntimeFoundationIssues.EmbeddedAgentRuntimeHostEpic));
        viewModel.RuntimeFoundation.Providers.Should().Contain(provider => !provider.RequiresExternalToolchain);
    }

    [Test]
    public void SettingsViewModelExposesUnifiedSettingsShellState()
    {
        using var workspace = TemporaryWorkbenchDirectory.Create();
        var runtimeFoundationCatalog = CreateRuntimeFoundationCatalog();
        var toolchainCenterCatalog = CreateToolchainCenterCatalog();
        var viewModel = new SettingsViewModel(
            new WorkbenchCatalog(runtimeFoundationCatalog, workspace.Root),
            runtimeFoundationCatalog,
            toolchainCenterCatalog);

        viewModel.SettingsIssueLabel.Should().Be(WorkbenchIssues.FormatIssueLabel(WorkbenchIssues.SettingsShell));
        viewModel.Categories.Should().HaveCountGreaterOrEqualTo(4);
        viewModel.SelectedCategory?.Key.Should().Be(WorkbenchSettingsCategoryKeys.Toolchains);
        viewModel.IsToolchainCenterVisible.Should().BeTrue();
        viewModel.ToolchainProviders.Should().HaveCount(3);
        viewModel.SelectedToolchainProviderSnapshot.Should().NotBeNull();
        viewModel.ToolchainWorkstreams.Should().NotBeEmpty();
        viewModel.ProviderSummary.Should().Contain("ready");
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
        return new RuntimeFoundationCatalog();
    }

    private static ToolchainCenterCatalog CreateToolchainCenterCatalog()
    {
        return new ToolchainCenterCatalog(TimeProvider.System, startBackgroundPolling: false);
    }
}
