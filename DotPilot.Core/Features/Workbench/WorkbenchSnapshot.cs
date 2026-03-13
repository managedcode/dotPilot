namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchSnapshot(
    string WorkspaceName,
    string WorkspaceRoot,
    string SearchPlaceholder,
    string SessionTitle,
    string SessionStage,
    string SessionSummary,
    IReadOnlyList<WorkbenchSessionEntry> SessionEntries,
    IReadOnlyList<WorkbenchRepositoryNode> RepositoryNodes,
    IReadOnlyList<WorkbenchDocumentDescriptor> Documents,
    IReadOnlyList<WorkbenchArtifactDescriptor> Artifacts,
    IReadOnlyList<WorkbenchLogEntry> Logs,
    IReadOnlyList<WorkbenchSettingsCategory> SettingsCategories);
