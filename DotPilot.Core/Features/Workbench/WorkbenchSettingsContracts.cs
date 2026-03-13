namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchSettingEntry(
    string Name,
    string Value,
    string Summary,
    bool IsSensitive,
    bool IsActionable);

public sealed record WorkbenchSettingsCategory(
    string Key,
    string Title,
    string Summary,
    IReadOnlyList<WorkbenchSettingEntry> Entries);
