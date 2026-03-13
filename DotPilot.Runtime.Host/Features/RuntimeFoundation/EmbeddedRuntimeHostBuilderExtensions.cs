using DotPilot.Core.Features.RuntimeFoundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans.Configuration;

namespace DotPilot.Runtime.Host.Features.RuntimeFoundation;

public static class EmbeddedRuntimeHostBuilderExtensions
{
    public static IHostBuilder UseDotPilotEmbeddedRuntime(
        this IHostBuilder builder,
        EmbeddedRuntimeHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var resolvedOptions = options ?? new EmbeddedRuntimeHostOptions();

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(resolvedOptions);
            services.AddSingleton<EmbeddedRuntimeHostCatalog>();
            services.AddSingleton<IEmbeddedRuntimeHostCatalog>(serviceProvider => serviceProvider.GetRequiredService<EmbeddedRuntimeHostCatalog>());
            services.AddHostedService<EmbeddedRuntimeHostLifecycleService>();
        });

        builder.UseOrleans((context, siloBuilder) =>
        {
            _ = context;
            ConfigureSilo(siloBuilder, resolvedOptions);
        });

        return builder;
    }

    internal static void ConfigureSilo(ISiloBuilder siloBuilder, EmbeddedRuntimeHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(siloBuilder);
        ArgumentNullException.ThrowIfNull(options);

        siloBuilder.UseLocalhostClustering(options.SiloPort, options.GatewayPort);
        siloBuilder.Configure<ClusterOptions>(cluster =>
        {
            cluster.ClusterId = options.ClusterId;
            cluster.ServiceId = options.ServiceId;
        });
        siloBuilder.AddMemoryGrainStorage(EmbeddedRuntimeHostNames.GrainStorageProviderName);
        siloBuilder.UseInMemoryReminderService();
    }
}
