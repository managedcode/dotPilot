namespace DotPilot.Core.Features.Workbench;

public enum WorkbenchDocumentViewMode
{
    Preview,
    DiffReview,
}

public enum WorkbenchDiffLineKind
{
    Context,
    Added,
    Removed,
}

public enum WorkbenchInspectorSection
{
    Artifacts,
    Logs,
}

public enum WorkbenchSessionEntryKind
{
    Operator,
    Agent,
    System,
}
