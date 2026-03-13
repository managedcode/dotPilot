namespace DotPilot.Core.Features.Workbench;

public static class WorkbenchSettingsCategoryKeys
{
    public const string Toolchains = "toolchains";
    public const string Providers = "providers";
    public const string Policies = "policies";
    public const string Storage = "storage";
}

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
