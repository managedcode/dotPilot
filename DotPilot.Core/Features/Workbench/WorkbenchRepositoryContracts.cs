namespace DotPilot.Core.Features.Workbench;

public sealed record WorkbenchRepositoryNode(
    string RelativePath,
    string DisplayLabel,
    string Name,
    int Depth,
    bool IsDirectory,
    bool CanOpen);
