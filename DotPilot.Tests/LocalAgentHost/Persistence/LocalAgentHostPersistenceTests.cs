using System.Net;
using System.Net.Sockets;
using DotPilot.Core.ControlPlaneDomain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotPilot.Tests.LocalAgentHost.Persistence;

public sealed class LocalAgentHostPersistenceTests
{
    [Test]
    public async Task AgentAndSessionGrainsPersistAcrossHostRestart()
    {
        var root = CreateRootPath();
        var clusterId = "dotpilot-test-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var serviceId = "dotpilot-service-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        var agentId = AgentProfileId.New();
        var sessionId = SessionId.New();

        try
        {
            var agentDescriptor = new AgentProfileDescriptor
            {
                Id = agentId,
                Name = "Persisted Grain Agent",
                Role = AgentRoleKind.Operator,
                ProviderId = ProviderId.New(),
                ModelRuntimeId = null,
                Tags = ["local"],
            };
            var sessionDescriptor = new SessionDescriptor
            {
                Id = sessionId,
                WorkspaceId = WorkspaceId.New(),
                Title = "Persisted Grain Session",
                Phase = SessionPhase.Execute,
                ApprovalState = ApprovalState.NotRequired,
                FleetId = null,
                AgentProfileIds = [agentId],
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            using (var firstHost = await StartHostAsync(CreateOptions(root, clusterId, serviceId)))
            {
                var grains = firstHost.Services.GetRequiredService<IGrainFactory>();
                await grains.GetGrain<IAgentProfileGrain>(agentId.ToString()).UpsertAsync(agentDescriptor);
                await grains.GetGrain<ISessionGrain>(sessionId.ToString()).UpsertAsync(sessionDescriptor);
            }

            using (var secondHost = await StartHostAsync(CreateOptions(root, clusterId, serviceId)))
            {
                var grains = secondHost.Services.GetRequiredService<IGrainFactory>();
                var reloadedAgent = await grains.GetGrain<IAgentProfileGrain>(agentId.ToString()).GetAsync();
                var reloadedSession = await grains.GetGrain<ISessionGrain>(sessionId.ToString()).GetAsync();

                reloadedAgent.Should().NotBeNull();
                reloadedAgent!.Name.Should().Be("Persisted Grain Agent");
                reloadedAgent.Tags.Should().ContainSingle("local");

                reloadedSession.Should().NotBeNull();
                reloadedSession!.Title.Should().Be("Persisted Grain Session");
                reloadedSession.AgentProfileIds.Should().ContainSingle(id => id == agentId);
            }

            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Should().NotBeEmpty();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static LocalAgentHostOptions CreateOptions(string root, string clusterId, string serviceId)
    {
        return new LocalAgentHostOptions
        {
            StorageBasePath = root,
            ClusterId = clusterId,
            ServiceId = serviceId,
            SiloPort = GetFreeTcpPort(),
            GatewayPort = GetFreeTcpPort(),
        };
    }

    private static async ValueTask<IHost> StartHostAsync(LocalAgentHostOptions options)
    {
        var host = Host.CreateDefaultBuilder()
            .UseDotPilotLocalAgentHost(options)
            .ConfigureLogging(logging => logging.ClearProviders())
            .Build();
        await host.StartAsync();
        return host;
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string CreateRootPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "DotPilot.Tests",
            nameof(LocalAgentHostPersistenceTests),
            Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
