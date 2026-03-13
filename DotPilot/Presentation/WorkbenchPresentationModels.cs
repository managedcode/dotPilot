namespace DotPilot.Presentation;

public sealed record WorkbenchRepositoryNodeItem(
    string RelativePath,
    string Name,
    string DisplayLabel,
    bool IsDirectory,
    bool CanOpen,
    string KindGlyph,
    Thickness IndentMargin,
    string AutomationId);

public sealed partial record WorkbenchSettingsCategoryItem(
    string Key,
    string Title,
    string Summary,
    string AutomationId,
    IReadOnlyList<WorkbenchSettingEntry> Entries);
