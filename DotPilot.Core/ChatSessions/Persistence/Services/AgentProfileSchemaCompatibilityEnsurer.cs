using System.Data;
using Microsoft.EntityFrameworkCore;

namespace DotPilot.Core.ChatSessions;

internal static class AgentProfileSchemaCompatibilityEnsurer
{
    private const string AgentProfilesTableName = "AgentProfiles";
    private const string ProviderPreferencesTableName = "ProviderPreferences";
    private const string ProviderLocalModelsTableName = "ProviderLocalModels";
    private const string DescriptionColumnName = "Description";
    private const string RoleColumnName = "Role";
    private const string CapabilitiesJsonColumnName = "CapabilitiesJson";
    private const string LocalModelPathColumnName = "LocalModelPath";
    private const string AddedAtColumnName = "AddedAt";

    public static async Task EnsureAsync(LocalAgentSessionDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        var agentProfileColumns = await ReadColumnNamesAsync(dbContext, AgentProfilesTableName, cancellationToken);
        if (!agentProfileColumns.Contains(DescriptionColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{AgentProfilesTableName}"
                 ADD COLUMN "{DescriptionColumnName}" TEXT NOT NULL DEFAULT '';
                 """,
                cancellationToken);
        }

        if (!agentProfileColumns.Contains(RoleColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{AgentProfilesTableName}"
                 ADD COLUMN "{RoleColumnName}" INTEGER NOT NULL DEFAULT {AgentProfileSchemaDefaults.DefaultRole};
                 """,
                cancellationToken);
        }

        if (!agentProfileColumns.Contains(CapabilitiesJsonColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{AgentProfilesTableName}"
                 ADD COLUMN "{CapabilitiesJsonColumnName}" TEXT NOT NULL DEFAULT '{AgentProfileSchemaDefaults.EmptyCapabilitiesJson}';
                 """,
                cancellationToken);
        }

        var providerPreferenceColumns = await ReadColumnNamesAsync(dbContext, ProviderPreferencesTableName, cancellationToken);
        if (!providerPreferenceColumns.Contains(LocalModelPathColumnName, StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 ALTER TABLE "{ProviderPreferencesTableName}"
                 ADD COLUMN "{LocalModelPathColumnName}" TEXT NULL;
                 """,
                cancellationToken);
        }

        var providerLocalModelColumns = await ReadColumnNamesAsync(dbContext, ProviderLocalModelsTableName, cancellationToken);
        if (providerLocalModelColumns.Count == 0)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                 CREATE TABLE IF NOT EXISTS "{ProviderLocalModelsTableName}" (
                     "ProviderKind" INTEGER NOT NULL,
                     "ModelPath" TEXT NOT NULL,
                     "{AddedAtColumnName}" TEXT NOT NULL,
                     CONSTRAINT "PK_{ProviderLocalModelsTableName}" PRIMARY KEY ("ProviderKind", "ModelPath")
                 );
                 """,
                cancellationToken);
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
             CREATE INDEX IF NOT EXISTS "IX_{ProviderLocalModelsTableName}_ProviderKind_{AddedAtColumnName}"
             ON "{ProviderLocalModelsTableName}" ("ProviderKind", "{AddedAtColumnName}");
             """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            $"""
             INSERT OR IGNORE INTO "{ProviderLocalModelsTableName}" ("ProviderKind", "ModelPath", "{AddedAtColumnName}")
             SELECT
                 "ProviderKind",
                 "{LocalModelPathColumnName}",
                 "{ProviderPreferencesTableName}"."UpdatedAt"
             FROM "{ProviderPreferencesTableName}"
             WHERE "{LocalModelPathColumnName}" IS NOT NULL
               AND TRIM("{LocalModelPathColumnName}") <> '';
             """,
            cancellationToken);
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(
        LocalAgentSessionDbContext dbContext,
        string tableName,
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
            command.CommandText = $"""PRAGMA table_info("{tableName}")""";

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
