using Microsoft.Extensions.Logging;

namespace DotPilot.Core.Workspace;

internal sealed class StartupWorkspaceHydration(
    IAgentWorkspaceState workspaceState,
    ILogger<StartupWorkspaceHydration> logger)
    : IStartupWorkspaceHydration, IDisposable
{
    private readonly SemaphoreSlim hydrationGate = new(1, 1);
    private readonly object stateSync = new();
    private bool isHydrating;
    private bool isReady;

    public bool IsHydrating
    {
        get
        {
            lock (stateSync)
            {
                return isHydrating;
            }
        }
    }

    public bool IsReady
    {
        get
        {
            lock (stateSync)
            {
                return isReady;
            }
        }
    }

    public event EventHandler? StateChanged;

    public void Dispose()
    {
        hydrationGate.Dispose();
    }

    public async ValueTask EnsureHydratedAsync(CancellationToken cancellationToken)
    {
        if (IsReady)
        {
            return;
        }

        await hydrationGate.WaitAsync(cancellationToken);
        try
        {
            if (IsReady)
            {
                return;
            }

            var hydrationSucceeded = false;
            UpdateState(isHydrating: true, isReady: false);
            StartupWorkspaceHydrationLog.HydrationStarted(logger);

            try
            {
                hydrationSucceeded = await TryHydrateWorkspaceAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                StartupWorkspaceHydrationLog.HydrationFailed(logger, exception);
            }
            finally
            {
                UpdateState(isHydrating: false, isReady: hydrationSucceeded);
            }
        }
        finally
        {
            hydrationGate.Release();
        }
    }

    private async ValueTask<bool> TryHydrateWorkspaceAsync(CancellationToken cancellationToken)
    {
        var workspace = await workspaceState.GetWorkspaceAsync(cancellationToken);
        if (workspace.IsFailed)
        {
            StartupWorkspaceHydrationLog.HydrationFailed(
                logger,
                new InvalidOperationException("Startup workspace hydration failed."));
            return false;
        }

        StartupWorkspaceHydrationLog.HydrationCompleted(logger);
        return true;
    }

    private void UpdateState(bool isHydrating, bool isReady)
    {
        var changed = false;
        lock (stateSync)
        {
            if (this.isHydrating != isHydrating)
            {
                this.isHydrating = isHydrating;
                changed = true;
            }

            if (this.isReady != isReady)
            {
                this.isReady = isReady;
                changed = true;
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
