using System.Data;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Core.ChatSessions;

internal static class AgentProfileSchemaCompatibilityEnsurer
{
    private const string AgentProfilesTableName = "AgentProfiles";
    private const string RoleColumnName = "Role";
    private const string CapabilitiesJsonColumnName = "CapabilitiesJson";

    public static async Task EnsureAsync(LocalAgentSessionDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var existingColumns = await ReadColumnNamesAsync(dbContext, cancellationToken);
        if (!existingColumns.Contains(RoleColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{AgentProfilesTableName}"
                 ADD COLUMN "{RoleColumnName}" INTEGER NOT NULL DEFAULT {AgentProfileSchemaDefaults.DefaultRole};
                 """,
                cancellationToken);
        }

        if (!existingColumns.Contains(CapabilitiesJsonColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{AgentProfilesTableName}"
                 ADD COLUMN "{CapabilitiesJsonColumnName}" TEXT NOT NULL DEFAULT '{AgentProfileSchemaDefaults.EmptyCapabilitiesJson}';
                 """,
                cancellationToken);
        }
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        LocalAgentSessionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{AgentProfilesTableName}")""";

            HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var nameOrdinal = reader.GetOrdinal("name");
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(nameOrdinal));
            }

            return columns;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
