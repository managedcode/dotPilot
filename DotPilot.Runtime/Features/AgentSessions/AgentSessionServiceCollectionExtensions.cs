using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotPilot.Runtime.Features.AgentSessions;

public static class AgentSessionServiceCollectionExtensions
{
    private const string DatabaseFileName = "dotpilot-agent-sessions.db";
    private const string DatabaseFolderName = "DotPilot";

    public static IServiceCollection AddAgentSessions(
        this IServiceCollection services,
        AgentSessionStorageOptions? storageOptions = null)
    {
        services.AddSingleton(storageOptions ?? new AgentSessionStorageOptions());
        services.AddDbContextFactory<LocalAgentSessionDbContext>(ConfigureDbContext);
        services.AddSingleton<DotPilot.Core.Features.AgentSessions.IAgentSessionService, AgentSessionService>();
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

        var databasePath = storageOptions.DatabasePath;
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            var rootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DatabaseFolderName);
            Directory.CreateDirectory(rootPath);
            databasePath = Path.Combine(rootPath, DatabaseFileName);
        }
        else
        {
            var databaseDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }
        }

        builder.UseSqlite($"Data Source={databasePath}");
    }
}
