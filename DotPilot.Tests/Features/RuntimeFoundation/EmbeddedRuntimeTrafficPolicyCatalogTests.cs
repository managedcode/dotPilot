using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotPilot.Tests.Features.RuntimeFoundation;

public sealed class EmbeddedRuntimeTrafficPolicyCatalogTests
{
    [Test]
    public void TrafficPolicyCatalogExposesExplicitTransitionsAndMermaidDiagram()
    {
        using var host = CreateHost();

        var snapshot = host.Services.GetRequiredService<IEmbeddedRuntimeTrafficPolicyCatalog>().GetSnapshot();

        snapshot.IssueNumber.Should().Be(RuntimeFoundationIssues.GrainTrafficPolicy);
        snapshot.AllowedTransitions.Should().Contain(transition =>
            transition.Source == "Session" &&
            transition.Target == "Artifact" &&
            transition.SourceMethods.Contains(nameof(ISessionGrain.UpsertAsync)) &&
            transition.TargetMethods.Contains(nameof(IArtifactGrain.UpsertAsync)));
        snapshot.MermaidDiagram.Should().Contain("flowchart LR");
        snapshot.MermaidDiagram.Should().Contain("Session --> Artifact");
    }

    [Test]
    public void TrafficPolicyCatalogAllowsConfiguredTransitionsAndRejectsUnsupportedHops()
    {
        using var host = CreateHost();
        var catalog = host.Services.GetRequiredService<IEmbeddedRuntimeTrafficPolicyCatalog>();

        var allowedDecision = catalog.Evaluate(
            new EmbeddedRuntimeTrafficProbe(
                typeof(ISessionGrain),
                nameof(ISessionGrain.UpsertAsync),
                typeof(IArtifactGrain),
                nameof(IArtifactGrain.UpsertAsync)));
        var deniedDecision = catalog.Evaluate(
            new EmbeddedRuntimeTrafficProbe(
                typeof(IPolicyGrain),
                nameof(IPolicyGrain.UpsertAsync),
                typeof(ISessionGrain),
                nameof(ISessionGrain.GetAsync)));

        allowedDecision.IsAllowed.Should().BeTrue();
        allowedDecision.MermaidDiagram.Should().Contain("Session ==> Artifact");
        deniedDecision.IsAllowed.Should().BeFalse();
        deniedDecision.MermaidDiagram.Should().Contain("Policy");
    }

    private static IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .UseDotPilotEmbeddedRuntime(new EmbeddedRuntimeHostOptions
            {
                ClusterId = $"dotpilot-traffic-{Guid.NewGuid():N}",
                ServiceId = $"dotpilot-traffic-service-{Guid.NewGuid():N}",
                SiloPort = GetFreeTcpPort(),
                GatewayPort = GetFreeTcpPort(),
            })
            .Build();
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
