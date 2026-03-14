using Microsoft.Extensions.Hosting;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

internal sealed class EmbeddedRuntimeHostLifecycleService(EmbeddedRuntimeHostCatalog catalog) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        catalog.SetState(DotPilot.Core.Features.RuntimeFoundation.EmbeddedRuntimeHostState.Stopped);
        return Task.CompletedTask;
    }
}
