namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchSessionEntry(
    string Title,
    string Timestamp,
    string Summary,
    WorkbenchSessionEntryKind Kind);
