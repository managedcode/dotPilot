using DotPilot.Core.Features.RuntimeFoundation;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Runtime.Features.RuntimeFoundation;

public static class RuntimeFoundationServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopRuntimeFoundation(
        this IServiceCollection services,
        RuntimePersistenceOptions? persistenceOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(persistenceOptions ?? new RuntimePersistenceOptions());
        services.AddSingleton<RuntimeSessionArchiveStore>();
        services.AddSingleton<IAgentRuntimeClient, AgentFrameworkRuntimeClient>();
        services.AddSingleton<IRuntimeFoundationCatalog, RuntimeFoundationCatalog>();
        return services;
    }

    public static IServiceCollection AddBrowserRuntimeFoundation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAgentRuntimeClient, DeterministicAgentRuntimeClient>();
        services.AddSingleton<IRuntimeFoundationCatalog, RuntimeFoundationCatalog>();
        return services;
    }
}
