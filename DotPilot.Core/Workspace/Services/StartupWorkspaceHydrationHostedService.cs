using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotPilot.Core.Workspace;

internal sealed class StartupWorkspaceHydrationHostedService(
    IStartupWorkspaceHydration startupWorkspaceHydration,
    ILogger<StartupWorkspaceHydrationHostedService> logger)
    : IHostedService
{
    private Task? hydrationTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        hydrationTask = RunHydrationAsync(cancellationToken);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (hydrationTask is null)
        {
            return;
        }

        try
        {
            await hydrationTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunHydrationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await startupWorkspaceHydration.EnsureHydratedAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            StartupWorkspaceHydrationHostedServiceLog.HydrationStartFailed(logger, exception);
        }
    }
}
