namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchDiffLine(
    WorkbenchDiffLineKind Kind,
    string Content);

public sealed record WorkbenchDocumentDescriptor(
    string RelativePath,
    string Title,
    string LanguageLabel,
    string RendererLabel,
    string StatusSummary,
    bool IsReadOnly,
    string PreviewContent,
    IReadOnlyList<WorkbenchDiffLine> DiffLines);
