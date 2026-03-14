using DotPilot.Core.Features.AgentSessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Runtime.Features.AgentSessions;

public static class AgentSessionServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSessions(
        this IServiceCollection services,
        AgentSessionStorageOptions? storageOptions = null)
    {
        services.AddLogging();
        services.AddSingleton(storageOptions ?? new AgentSessionStorageOptions());
        services.AddDbContextFactory<LocalAgentSessionDbContext>(ConfigureDbContext);
        services.AddSingleton<LocalAgentSessionStateStore>();
        services.AddSingleton<LocalAgentChatHistoryStore>();
        services.AddSingleton<AgentProviderStatusCache>();
        services.AddSingleton<IAgentProviderStatusCache>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentProviderStatusCache>());
        services.AddSingleton<AgentRuntimeConversationFactory>();
        services.AddSingleton<DotPilot.Core.Features.AgentSessions.IAgentSessionService, AgentSessionService>();
        services.AddSingleton<IAgentWorkspaceState, AgentWorkspaceState>();
        return services;
    }

    private static void ConfigureDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder builder)
    {
        var storageOptions = serviceProvider.GetRequiredService<AgentSessionStorageOptions>();

        if (OperatingSystem.IsBrowser() || storageOptions.UseInMemoryDatabase)
        {
            builder.UseInMemoryDatabase(storageOptions.InMemoryDatabaseName);
            return;
        }

        var databasePath = AgentSessionStoragePaths.ResolveDatabasePath(storageOptions);
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        builder.UseSqlite($"Data Source={databasePath}");
    }
}
