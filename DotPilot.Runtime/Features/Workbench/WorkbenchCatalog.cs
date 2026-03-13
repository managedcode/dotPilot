using DotPilot.Core.Features.RuntimeFoundation;
using DotPilot.Core.Features.Workbench;

namespace DotPilot.Runtime.Features.Workbench;

public sealed class WorkbenchCatalog : IWorkbenchCatalog
{
    private readonly IRuntimeFoundationCatalog _runtimeFoundationCatalog;
    private readonly string? _workspaceRootOverride;

    public WorkbenchCatalog(IRuntimeFoundationCatalog runtimeFoundationCatalog)
        : this(runtimeFoundationCatalog, workspaceRootOverride: null)
    {
    }

    public WorkbenchCatalog(IRuntimeFoundationCatalog runtimeFoundationCatalog, string? workspaceRootOverride)
    {
        ArgumentNullException.ThrowIfNull(runtimeFoundationCatalog);
        _runtimeFoundationCatalog = runtimeFoundationCatalog;
        _workspaceRootOverride = workspaceRootOverride;
    }

    public WorkbenchSnapshot GetSnapshot()
    {
        var runtimeFoundationSnapshot = _runtimeFoundationCatalog.GetSnapshot();
        var workspace = WorkbenchWorkspaceResolver.Resolve(_workspaceRootOverride);
        return workspace.IsAvailable
            ? new WorkbenchWorkspaceSnapshotBuilder(workspace, runtimeFoundationSnapshot).Build()
            : WorkbenchSeedData.Create(runtimeFoundationSnapshot);
    }
}
