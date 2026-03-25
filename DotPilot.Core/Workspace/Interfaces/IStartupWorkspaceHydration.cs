namespace DotPilot.Core.Workspace.Interfaces;

public interface IStartupWorkspaceHydration
{
    bool IsHydrating { get; }

    bool HasCompletedInitialAttempt { get; }

    bool IsReady { get; }

    event EventHandler? StateChanged;

    ValueTask EnsureHydratedAsync(CancellationToken cancellationToken);
}
