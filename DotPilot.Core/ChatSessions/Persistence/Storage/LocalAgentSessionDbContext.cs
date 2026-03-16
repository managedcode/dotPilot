using Microsoft.EntityFrameworkCore;

namespace DotPilot.Core.ChatSessions;

internal sealed class LocalAgentSessionDbContext(DbContextOptions<LocalAgentSessionDbContext> options)
    : DbContext(options)
{
    public DbSet<AgentProfileRecord> AgentProfiles => Set<AgentProfileRecord>();

    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();

    public DbSet<SessionEntryRecord> SessionEntries => Set<SessionEntryRecord>();

    public DbSet<ProviderPreferenceRecord> ProviderPreferences => Set<ProviderPreferenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentProfileRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Name).IsRequired();
            entity.Property(record => record.Description)
                .HasDefaultValue(string.Empty)
                .IsRequired();
            entity.Property(record => record.Role)
                .HasDefaultValue(AgentProfileSchemaDefaults.DefaultRole);
            entity.Property(record => record.ModelName).IsRequired();
            entity.Property(record => record.SystemPrompt).IsRequired();
            entity.Property(record => record.CapabilitiesJson)
                .HasDefaultValue(AgentProfileSchemaDefaults.EmptyCapabilitiesJson)
                .IsRequired();
        });

        modelBuilder.Entity<SessionRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Title).IsRequired();
            entity.HasIndex(record => record.UpdatedAt);
        });

        modelBuilder.Entity<SessionEntryRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Author).IsRequired();
            entity.Property(record => record.Text).IsRequired();
            entity.HasIndex(record => new { record.SessionId, record.Timestamp });
        });

        modelBuilder.Entity<ProviderPreferenceRecord>(entity =>
        {
            entity.HasKey(record => record.ProviderKind);
            entity.Property(record => record.ProviderKind).ValueGeneratedNever();
        });
    }
}
