using DotPilot.Core.AgentBuilder;
using DotPilot.Core.Providers;
using DotPilot.Core.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Core.ChatSessions;

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
        services.AddSingleton<LocalCodexThreadStateStore>();
        services.AddSingleton<IAgentProviderStatusReader, AgentProviderStatusReader>();
        services.AddSingleton<AgentPromptDraftGenerator>();
        services.AddSingleton<AgentExecutionLoggingMiddleware>();
        services.AddSingleton<ISessionActivityMonitor, SessionActivityMonitor>();
        services.AddSingleton<AgentRuntimeConversationFactory>();
        services.AddSingleton<IAgentSessionService, AgentSessionService>();
        services.AddSingleton<IAgentWorkspaceState, AgentWorkspaceState>();
        services.AddSingleton<IStartupWorkspaceHydration, StartupWorkspaceHydration>();
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
