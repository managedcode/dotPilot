using DotPilot.Runtime.Features.Workbench;

namespace DotPilot.Tests.Features.Workbench;

public class WorkbenchCatalogTests
{
    [Test]
    public void GetSnapshotUsesLiveWorkspaceAndRespectsIgnoreRules()
    {
        using var workspace = TemporaryWorkbenchDirectory.Create();

        var snapshot = CreateWorkbenchCatalog(workspace.Root).GetSnapshot();

        snapshot.WorkspaceRoot.Should().Be(workspace.Root);
        snapshot.RepositoryNodes.Should().Contain(node => node.RelativePath == "src/MainPage.xaml");
        snapshot.RepositoryNodes.Should().Contain(node => node.RelativePath == "src/SettingsPage.xaml");
        snapshot.RepositoryNodes.Should().NotContain(node => node.RelativePath.Contains("ignored", StringComparison.OrdinalIgnoreCase));
        snapshot.RepositoryNodes.Should().NotContain(node => node.RelativePath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        snapshot.Documents.Should().Contain(document => document.RelativePath == "src/MainPage.xaml");
        snapshot.SettingsCategories.Should().Contain(category => category.Key == "providers");
        snapshot.Logs.Should().HaveCount(4);
    }

    [Test]
    public void GetSnapshotFallsBackToSeededDataWhenWorkspaceHasNoSupportedDocuments()
    {
        using var workspace = TemporaryWorkbenchDirectory.Create(includeSupportedFiles: false);

        var snapshot = CreateWorkbenchCatalog(workspace.Root).GetSnapshot();

        snapshot.WorkspaceName.Should().Be("Browser sandbox");
        snapshot.Documents.Should().NotBeEmpty();
        snapshot.RepositoryNodes.Should().Contain(node => node.RelativePath == "DotPilot/Presentation/MainPage.xaml");
    }

    private static WorkbenchCatalog CreateWorkbenchCatalog(string workspaceRoot)
    {
        return new WorkbenchCatalog(CreateRuntimeFoundationCatalog(), workspaceRoot);
    }

    private static RuntimeFoundationCatalog CreateRuntimeFoundationCatalog()
    {
        return new RuntimeFoundationCatalog();
    }
}
