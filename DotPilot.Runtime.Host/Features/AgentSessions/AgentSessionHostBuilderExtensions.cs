using ManagedCode.Storage.FileSystem;
using ManagedCode.Storage.FileSystem.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;

namespace DotPilot.Runtime.Host.Features.AgentSessions;

public static class AgentSessionHostBuilderExtensions
{
    public static IHostBuilder UseDotPilotAgentSessions(
        this IHostBuilder builder,
        AgentSessionHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resolvedOptions = options ?? new AgentSessionHostOptions();
        builder.UseOrleans((context, siloBuilder) =>
        {
            _ = context;
            ConfigureSilo(siloBuilder, resolvedOptions);
        });

        return builder;
    }

    internal static void ConfigureSilo(ISiloBuilder siloBuilder, AgentSessionHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(siloBuilder);
        ArgumentNullException.ThrowIfNull(options);

        siloBuilder.UseLocalhostClustering(options.SiloPort, options.GatewayPort);
        siloBuilder.Configure<ClusterOptions>(cluster =>
        {
            cluster.ClusterId = options.ClusterId;
            cluster.ServiceId = options.ServiceId;
        });
        siloBuilder.ConfigureServices(services =>
        {
            services.AddFileSystemStorageAsDefault(storage =>
            {
                storage.BaseFolder = AgentSessionHostStoragePaths.ResolveStorageBasePath(options);
            });
        });
        siloBuilder.AddGrainStorage<IFileSystemStorage>(AgentSessionHostNames.GrainStorageProviderName, storage =>
        {
            storage.StateDirectory = options.GrainStateDirectory;
        });
        siloBuilder.UseInMemoryReminderService();
    }
}
