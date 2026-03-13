using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotPilot.Tests.Features.RuntimeFoundation;

public class EmbeddedRuntimeHostTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 3, 13, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void CatalogStartsInStoppedStateBeforeTheHostRuns()
    {
        var options = CreateOptions();
        using var host = CreateHost(options);

        var snapshot = host.Services.GetRequiredService<IEmbeddedRuntimeHostCatalog>().GetSnapshot();

        snapshot.State.Should().Be(EmbeddedRuntimeHostState.Stopped);
        snapshot.ClusteringMode.Should().Be(EmbeddedRuntimeClusteringMode.Localhost);
        snapshot.GrainStorageMode.Should().Be(EmbeddedRuntimeStorageMode.InMemory);
        snapshot.ReminderStorageMode.Should().Be(EmbeddedRuntimeStorageMode.InMemory);
        snapshot.ClusterId.Should().Be(options.ClusterId);
        snapshot.ServiceId.Should().Be(options.ServiceId);
        snapshot.SiloPort.Should().Be(options.SiloPort);
        snapshot.GatewayPort.Should().Be(options.GatewayPort);
        snapshot.Grains.Select(grain => grain.Name).Should().ContainInOrder("Session", "Workspace", "Fleet", "Policy", "Artifact");
    }

    [Test]
    public void CatalogUsesDefaultLocalhostOptionsWhenTheCallerDoesNotProvideOverrides()
    {
        var defaults = new EmbeddedRuntimeHostOptions();
        using var host = Host.CreateDefaultBuilder()
            .UseDotPilotEmbeddedRuntime()
            .Build();

        var snapshot = host.Services.GetRequiredService<IEmbeddedRuntimeHostCatalog>().GetSnapshot();

        snapshot.State.Should().Be(EmbeddedRuntimeHostState.Stopped);
        snapshot.ClusteringMode.Should().Be(EmbeddedRuntimeClusteringMode.Localhost);
        snapshot.GrainStorageMode.Should().Be(EmbeddedRuntimeStorageMode.InMemory);
        snapshot.ReminderStorageMode.Should().Be(EmbeddedRuntimeStorageMode.InMemory);
        snapshot.ClusterId.Should().Be(defaults.ClusterId);
        snapshot.ServiceId.Should().Be(defaults.ServiceId);
        snapshot.SiloPort.Should().Be(defaults.SiloPort);
        snapshot.GatewayPort.Should().Be(defaults.GatewayPort);
    }

    [Test]
    public async Task CatalogTransitionsToRunningStateAfterHostStartAsync()
    {
        var options = CreateOptions();
        using var host = CreateHost(options);

        await host.StartAsync();
        var snapshot = host.Services.GetRequiredService<IEmbeddedRuntimeHostCatalog>().GetSnapshot();

        snapshot.State.Should().Be(EmbeddedRuntimeHostState.Running);
    }

    [Test]
    public async Task InitialGrainsReturnNullBeforeTheirFirstWrite()
    {
        var options = CreateOptions();
        await RunWithStartedHostAsync(
            options,
            async host =>
            {
                var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

                (await grainFactory.GetGrain<ISessionGrain>(SessionId.New().ToString()).GetAsync()).Should().BeNull();
                (await grainFactory.GetGrain<IWorkspaceGrain>(WorkspaceId.New().ToString()).GetAsync()).Should().BeNull();
                (await grainFactory.GetGrain<IFleetGrain>(FleetId.New().ToString()).GetAsync()).Should().BeNull();
                (await grainFactory.GetGrain<IPolicyGrain>(PolicyId.New().ToString()).GetAsync()).Should().BeNull();
                (await grainFactory.GetGrain<IArtifactGrain>(ArtifactId.New().ToString()).GetAsync()).Should().BeNull();
            });
    }

    [Test]
    public async Task InitialGrainsRoundTripTheirDescriptorState()
    {
        var workspace = CreateWorkspace();
        var firstAgentId = AgentProfileId.New();
        var secondAgentId = AgentProfileId.New();
        var fleet = CreateFleet(firstAgentId, secondAgentId);
        var session = CreateSession(workspace.Id, fleet.Id, firstAgentId, secondAgentId);
        var policy = CreatePolicy();
        var artifact = CreateArtifact(session.Id, firstAgentId);
        var options = CreateOptions();
        await RunWithStartedHostAsync(
            options,
            async host =>
            {
                var grainFactory = host.Services.GetRequiredService<IGrainFactory>();

                (await grainFactory.GetGrain<ISessionGrain>(session.Id.ToString()).UpsertAsync(session)).Should().BeEquivalentTo(session);
                (await grainFactory.GetGrain<IWorkspaceGrain>(workspace.Id.ToString()).UpsertAsync(workspace)).Should().BeEquivalentTo(workspace);
                (await grainFactory.GetGrain<IFleetGrain>(fleet.Id.ToString()).UpsertAsync(fleet)).Should().BeEquivalentTo(fleet);
                (await grainFactory.GetGrain<IPolicyGrain>(policy.Id.ToString()).UpsertAsync(policy)).Should().BeEquivalentTo(policy);
                (await grainFactory.GetGrain<IArtifactGrain>(artifact.Id.ToString()).UpsertAsync(artifact)).Should().BeEquivalentTo(artifact);

                (await grainFactory.GetGrain<ISessionGrain>(session.Id.ToString()).GetAsync()).Should().BeEquivalentTo(session);
                (await grainFactory.GetGrain<IWorkspaceGrain>(workspace.Id.ToString()).GetAsync()).Should().BeEquivalentTo(workspace);
                (await grainFactory.GetGrain<IFleetGrain>(fleet.Id.ToString()).GetAsync()).Should().BeEquivalentTo(fleet);
                (await grainFactory.GetGrain<IPolicyGrain>(policy.Id.ToString()).GetAsync()).Should().BeEquivalentTo(policy);
                (await grainFactory.GetGrain<IArtifactGrain>(artifact.Id.ToString()).GetAsync()).Should().BeEquivalentTo(artifact);
            });
    }

    [Test]
    public async Task SessionGrainRejectsDescriptorIdsThatDoNotMatchThePrimaryKey()
    {
        var workspace = CreateWorkspace();
        var firstAgentId = AgentProfileId.New();
        var secondAgentId = AgentProfileId.New();
        var fleet = CreateFleet(firstAgentId, secondAgentId);
        var session = CreateSession(workspace.Id, fleet.Id, firstAgentId, secondAgentId);
        var options = CreateOptions();
        await RunWithStartedHostAsync(
            options,
            async host =>
            {
                var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
                var mismatchedGrain = grainFactory.GetGrain<ISessionGrain>(SessionId.New().ToString());

                var action = async () => await mismatchedGrain.UpsertAsync(session);

                await action.Should().ThrowAsync<ArgumentException>();
            });
    }

    [Test]
    public async Task SessionStateDoesNotSurviveHostRestartWhenUsingInMemoryStorage()
    {
        var workspace = CreateWorkspace();
        var firstAgentId = AgentProfileId.New();
        var secondAgentId = AgentProfileId.New();
        var fleet = CreateFleet(firstAgentId, secondAgentId);
        var session = CreateSession(workspace.Id, fleet.Id, firstAgentId, secondAgentId);

        await RunWithStartedHostAsync(
            CreateOptions(),
            async firstHost =>
            {
                var firstFactory = firstHost.Services.GetRequiredService<IGrainFactory>();
                await firstFactory.GetGrain<ISessionGrain>(session.Id.ToString()).UpsertAsync(session);
                (await firstFactory.GetGrain<ISessionGrain>(session.Id.ToString()).GetAsync()).Should().BeEquivalentTo(session);
            });

        await RunWithStartedHostAsync(
            CreateOptions(),
            async secondHost =>
            {
                var secondFactory = secondHost.Services.GetRequiredService<IGrainFactory>();
                (await secondFactory.GetGrain<ISessionGrain>(session.Id.ToString()).GetAsync()).Should().BeNull();
            });
    }

    private static IHost CreateHost(EmbeddedRuntimeHostOptions options)
    {
        return Host.CreateDefaultBuilder()
            .UseDotPilotEmbeddedRuntime(options)
            .Build();
    }

    private static async Task RunWithStartedHostAsync(EmbeddedRuntimeHostOptions options, Func<IHost, Task> assertion)
    {
        using var host = CreateHost(options);
        await host.StartAsync();

        try
        {
            await assertion(host);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private static EmbeddedRuntimeHostOptions CreateOptions()
    {
        return new EmbeddedRuntimeHostOptions
        {
            ClusterId = $"dotpilot-local-{Guid.NewGuid():N}",
            ServiceId = $"dotpilot-service-{Guid.NewGuid():N}",
            SiloPort = GetFreeTcpPort(),
            GatewayPort = GetFreeTcpPort(),
        };
    }

    private static WorkspaceDescriptor CreateWorkspace()
    {
        return new WorkspaceDescriptor
        {
            Id = WorkspaceId.New(),
            Name = "dotPilot",
            RootPath = "/repo/dotPilot",
            BranchName = "codex/issue-24-embedded-orleans-host",
        };
    }

    private static FleetDescriptor CreateFleet(AgentProfileId firstAgentId, AgentProfileId secondAgentId)
    {
        return new FleetDescriptor
        {
            Id = FleetId.New(),
            Name = "Local Runtime Fleet",
            ExecutionMode = FleetExecutionMode.Orchestrated,
            AgentProfileIds = [firstAgentId, secondAgentId],
        };
    }

    private static SessionDescriptor CreateSession(
        WorkspaceId workspaceId,
        FleetId fleetId,
        AgentProfileId firstAgentId,
        AgentProfileId secondAgentId)
    {
        return new SessionDescriptor
        {
            Id = SessionId.New(),
            WorkspaceId = workspaceId,
            Title = "Embedded Orleans runtime host test",
            Phase = SessionPhase.Execute,
            ApprovalState = ApprovalState.Pending,
            FleetId = fleetId,
            AgentProfileIds = [firstAgentId, secondAgentId],
            CreatedAt = Timestamp,
            UpdatedAt = Timestamp,
        };
    }

    private static PolicyDescriptor CreatePolicy()
    {
        return new PolicyDescriptor
        {
            Id = PolicyId.New(),
            Name = "Desktop Local Policy",
            DefaultApprovalState = ApprovalState.Pending,
            AllowsNetworkAccess = false,
            AllowsFileSystemWrites = true,
            ProtectedScopes = [ApprovalScope.CommandExecution, ApprovalScope.FileWrite],
        };
    }

    private static ArtifactDescriptor CreateArtifact(SessionId sessionId, AgentProfileId agentProfileId)
    {
        return new ArtifactDescriptor
        {
            Id = ArtifactId.New(),
            SessionId = sessionId,
            AgentProfileId = agentProfileId,
            Name = "runtime-foundation.snapshot.json",
            Kind = ArtifactKind.Snapshot,
            RelativePath = "artifacts/runtime-foundation.snapshot.json",
            CreatedAt = Timestamp,
        };
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
