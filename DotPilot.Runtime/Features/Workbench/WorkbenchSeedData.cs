using DotPilot.Core.Features.RuntimeFoundation;
using DotPilot.Core.Features.Workbench;

namespace DotPilot.Runtime.Features.Workbench;

internal static class WorkbenchSeedData
{
    private const string WorkspaceName = "Browser sandbox";
    private const string WorkspaceRoot = "Seeded browser-safe workspace";
    private const string SearchPlaceholder = "Search the workspace tree";
    private const string SessionTitle = "Issue #13 workbench slice";
    private const string SessionStage = "Review";
    private const string SessionSummary =
        "Seeded workbench data keeps browser automation deterministic while the desktop host can use the real repository.";
    private const string MonacoRendererLabel = "Monaco-aligned preview";
    private const string ReadOnlyStatusSummary = "Read-only workspace reference";
    private const string DiffReviewNote = "workbench review baseline";
    private const string ProviderCategoryKey = "providers";
    private const string PolicyCategoryKey = "policies";
    private const string StorageCategoryKey = "storage";
    private const string ProviderCategoryTitle = "Providers";
    private const string PolicyCategoryTitle = "Policies";
    private const string StorageCategoryTitle = "Storage";
    private const string ProviderCategorySummary = "Provider toolchains and runtime readiness";
    private const string PolicyCategorySummary = "Approval and review defaults";
    private const string StorageCategorySummary = "Workspace and artifact retention";
    private const string ReviewPath = "docs/Features/workbench-foundation.md";
    private const string PlanPath = "issue-13-workbench-foundation.plan.md";
    private const string MainPagePath = "DotPilot/Presentation/MainPage.xaml";
    private const string SettingsPath = "DotPilot/Presentation/SettingsPage.xaml";
    private const string ArchitecturePath = "docs/Architecture.md";
    private const string ArtifactsRelativePath = "artifacts/workbench-shell.png";
    private const string SessionOutputPath = "artifacts/session-output.log";
    private const string CurrentWorkspaceEntryName = "Current workspace";
    private const string ArtifactRetentionEntryName = "Artifact retention";
    private const string ApprovalModeEntryName = "Approval mode";
    private const string ReviewGateEntryName = "Diff review gate";
    private const string ReviewGateEntryValue = "Required";
    private const string ArtifactRetentionEntryValue = "14 days";
    private const string ApprovalModeEntryValue = "Operator confirmation";
    private const string TimestampOne = "09:10";
    private const string TimestampTwo = "09:12";
    private const string TimestampThree = "09:14";
    private const string TimestampFour = "09:15";
    private const string InfoLevel = "INFO";
    private const string ReviewLevel = "REVIEW";
    private const string AgentSource = "design-agent";
    private const string RuntimeSource = "runtime";
    private const string SettingsSource = "settings";
    private const string ReviewMessage = "Prepared the workbench shell review with repository navigation, diff mode, and settings coverage.";
    private const string IndexMessage = "Loaded the browser-safe seeded workspace.";
    private const string DiffMessage = "Queued a review diff for the primary workbench page.";
    private const string SettingsMessage = "Published the unified settings shell categories.";
    public static WorkbenchSnapshot Create(RuntimeFoundationSnapshot runtimeFoundationSnapshot)
    {
        ArgumentNullException.ThrowIfNull(runtimeFoundationSnapshot);

        var repositoryNodes = CreateRepositoryNodes();
        var documents = CreateDocuments();

        return new(
            WorkspaceName,
            WorkspaceRoot,
            SearchPlaceholder,
            SessionTitle,
            SessionStage,
            SessionSummary,
            CreateSessionEntries(),
            repositoryNodes,
            documents,
            CreateArtifacts(),
            CreateLogs(),
            CreateSettingsCategories(runtimeFoundationSnapshot));
    }

    private static IReadOnlyList<WorkbenchRepositoryNode> CreateRepositoryNodes()
    {
        return
        [
            new("docs", "docs", "docs", 0, true, false),
            new(ArchitecturePath, ArchitecturePath, "Architecture.md", 1, false, true),
            new(ReviewPath, ReviewPath, "workbench-foundation.md", 1, false, true),
            new("DotPilot", "DotPilot", "DotPilot", 0, true, false),
            new("DotPilot/Presentation", "DotPilot/Presentation", "Presentation", 1, true, false),
            new(MainPagePath, MainPagePath, "MainPage.xaml", 2, false, true),
            new(SettingsPath, SettingsPath, "SettingsPage.xaml", 2, false, true),
            new(PlanPath, PlanPath, "issue-13-workbench-foundation.plan.md", 0, false, true),
        ];
    }

    private static IReadOnlyList<WorkbenchDocumentDescriptor> CreateDocuments()
    {
        return
        [
            CreateDocument(
                MainPagePath,
                "MainPage.xaml",
                "XAML",
                MonacoRendererLabel,
                ReadOnlyStatusSummary,
                isReadOnly: true,
                """
                <Grid Margin="12"
                      ColumnSpacing="20">
                  <controls:WorkbenchSidebar />
                  <controls:WorkbenchDocumentSurface Grid.Column="1" />
                  <controls:WorkbenchInspectorPanel Grid.Column="2" />
                </Grid>
                """),
            CreateDocument(
                SettingsPath,
                "SettingsPage.xaml",
                "XAML",
                MonacoRendererLabel,
                ReadOnlyStatusSummary,
                isReadOnly: true,
                """
                <Grid Margin="12"
                      ColumnSpacing="20">
                  <controls:AgentSidebar />
                  <controls:SettingsShell Grid.Column="1" />
                </Grid>
                """),
            CreateDocument(
                ReviewPath,
                "workbench-foundation.md",
                "Markdown",
                MonacoRendererLabel,
                ReadOnlyStatusSummary,
                isReadOnly: true,
                """
                # Workbench Foundation

                Epic #13 keeps the current desktop shell while replacing sample data with a repository tree,
                a file surface, an artifact dock, and a unified settings shell.
                """),
        ];
    }

    private static WorkbenchDocumentDescriptor CreateDocument(
        string relativePath,
        string title,
        string languageLabel,
        string rendererLabel,
        string statusSummary,
        bool isReadOnly,
        string previewContent)
    {
        return new(
            relativePath,
            title,
            languageLabel,
            rendererLabel,
            statusSummary,
            isReadOnly,
            previewContent,
            CreateDiffLines(title, relativePath));
    }

    private static IReadOnlyList<WorkbenchArtifactDescriptor> CreateArtifacts()
    {
        return
        [
            new("Workbench feature doc", "Documentation", "Ready", ReviewPath, "Tracks epic #13 scope, flow, and verification."),
            new("Workbench implementation plan", "Plan", "Ready", PlanPath, "Records ordered implementation and validation work."),
            new("Workbench shell proof", "Screenshot", "Queued", ArtifactsRelativePath, "Reserved for browser UI test screenshots."),
            new("Session output", "Console", "Streaming", SessionOutputPath, "The runtime console stays attached to the current workbench."),
        ];
    }

    private static IReadOnlyList<WorkbenchLogEntry> CreateLogs()
    {
        return
        [
            new(TimestampOne, InfoLevel, RuntimeSource, IndexMessage),
            new(TimestampTwo, ReviewLevel, AgentSource, ReviewMessage),
            new(TimestampThree, ReviewLevel, RuntimeSource, DiffMessage),
            new(TimestampFour, InfoLevel, SettingsSource, SettingsMessage),
        ];
    }

    private static IReadOnlyList<WorkbenchSessionEntry> CreateSessionEntries()
    {
        return
        [
            new("Plan baseline", TimestampOne, "Locked the issue #13 workbench plan and preserved the green solution baseline.", WorkbenchSessionEntryKind.Operator),
            new("Tree indexed", TimestampTwo, "Loaded a deterministic repository tree for browser-hosted validation.", WorkbenchSessionEntryKind.System),
            new("Diff review", TimestampThree, "Prepared the MainPage review surface with a Monaco-aligned preview and diff mode.", WorkbenchSessionEntryKind.Agent),
            new("Settings shell", TimestampFour, "Published providers, policies, and storage categories as a first-class route.", WorkbenchSessionEntryKind.System),
        ];
    }

    private static IReadOnlyList<WorkbenchSettingsCategory> CreateSettingsCategories(RuntimeFoundationSnapshot runtimeFoundationSnapshot)
    {
        return
        [
            new(
                ProviderCategoryKey,
                ProviderCategoryTitle,
                ProviderCategorySummary,
                runtimeFoundationSnapshot.Providers
                    .Select(provider => new WorkbenchSettingEntry(
                        provider.DisplayName,
                        provider.Status.ToString(),
                        provider.StatusSummary,
                        IsSensitive: false,
                        IsActionable: provider.RequiresExternalToolchain))
                    .ToArray()),
            new(
                PolicyCategoryKey,
                PolicyCategoryTitle,
                PolicyCategorySummary,
                [
                    new(ApprovalModeEntryName, ApprovalModeEntryValue, "All file and tool changes stay operator-approved.", IsSensitive: false, IsActionable: true),
                    new(ReviewGateEntryName, ReviewGateEntryValue, "Agent proposals must stay reviewable before acceptance.", IsSensitive: false, IsActionable: true),
                ]),
            new(
                StorageCategoryKey,
                StorageCategoryTitle,
                StorageCategorySummary,
                [
                    new(CurrentWorkspaceEntryName, WorkspaceRoot, "Browser-hosted automation uses seeded workspace metadata.", IsSensitive: false, IsActionable: false),
                    new(ArtifactRetentionEntryName, ArtifactRetentionEntryValue, "Artifacts stay visible from the main workbench dock.", IsSensitive: false, IsActionable: true),
                ]),
        ];
    }

    private static IReadOnlyList<WorkbenchDiffLine> CreateDiffLines(string title, string relativePath)
    {
        return
        [
            new(WorkbenchDiffLineKind.Context, $"@@ {relativePath} @@"),
            new(WorkbenchDiffLineKind.Removed, $"- prototype-only state for {title}"),
            new(WorkbenchDiffLineKind.Added, $"+ runtime-backed workbench state for {DiffReviewNote}"),
        ];
    }
}
