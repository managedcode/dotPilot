using DotPilot.Core.Features.ToolchainCenter;

namespace DotPilot.Presentation;

public sealed record WorkbenchRepositoryNodeItem(
    string RelativePath,
    string Name,
    string DisplayLabel,
    bool IsDirectory,
    bool CanOpen,
    string KindGlyph,
    Thickness IndentMargin,
    string AutomationId,
    string TapAutomationId);

public sealed partial record WorkbenchSettingsCategoryItem(
    string Key,
    string Title,
    string Summary,
    string AutomationId,
    IReadOnlyList<WorkbenchSettingEntry> Entries);

public sealed record ToolchainProviderItem(
    ToolchainProviderSnapshot Snapshot,
    string AutomationId)
{
    public string DisplayName => Snapshot.Provider.DisplayName;

    public string SectionLabel => Snapshot.SectionLabel;

    public string ReadinessLabel => Snapshot.ReadinessState.ToString();

    public string ReadinessSummary => Snapshot.ReadinessSummary;
}

public sealed record ToolchainWorkstreamItem(
    ToolchainCenterWorkstreamDescriptor Workstream,
    string AutomationId);
