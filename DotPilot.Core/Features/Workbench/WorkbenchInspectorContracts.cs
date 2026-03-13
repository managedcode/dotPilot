namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchArtifactDescriptor(
    string Name,
    string Kind,
    string Status,
    string RelativePath,
    string Summary);

public sealed record WorkbenchLogEntry(
    string Timestamp,
    string Level,
    string Source,
    string Message);
