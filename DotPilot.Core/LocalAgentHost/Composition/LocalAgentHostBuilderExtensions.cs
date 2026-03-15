using ManagedCode.Storage.FileSystem;
using ManagedCode.Storage.FileSystem.Extensions;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;

namespace DotPilot.Core.LocalAgentHost;

public static class LocalAgentHostBuilderExtensions
{
    public static IHostBuilder UseDotPilotLocalAgentHost(
        this IHostBuilder builder,
        LocalAgentHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resolvedOptions = options ?? new LocalAgentHostOptions();
        builder.UseOrleans((context, siloBuilder) =>
        {
            _ = context;
            ConfigureSilo(siloBuilder, resolvedOptions);
        });

        return builder;
    }

    internal static void ConfigureSilo(ISiloBuilder siloBuilder, LocalAgentHostOptions options)
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
                storage.BaseFolder = LocalAgentHostStoragePaths.ResolveStorageBasePath(options);
            });
        });
        siloBuilder.AddGrainStorage<IFileSystemStorage>(LocalAgentHostNames.GrainStorageProviderName, storage =>
        {
            storage.StateDirectory = options.GrainStateDirectory;
        });
        siloBuilder.UseInMemoryReminderService();
    }
}
