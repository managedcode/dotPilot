using Microsoft.EntityFrameworkCore;

namespace DotPilot.Runtime.Features.AgentSessions;

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
            entity.Property(record => record.ModelName).IsRequired();
            entity.Property(record => record.SystemPrompt).IsRequired();
            entity.Property(record => record.CapabilitiesJson).IsRequired();
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

internal sealed class AgentProfileRecord
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Role { get; set; }

    public int ProviderKind { get; set; }

    public string ModelName { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public string CapabilitiesJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class SessionRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid PrimaryAgentProfileId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class SessionEntryRecord
{
    public string Id { get; set; } = string.Empty;

    public Guid SessionId { get; set; }

    public Guid? AgentProfileId { get; set; }

    public int Kind { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string? AccentLabel { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}

internal sealed class ProviderPreferenceRecord
{
    public int ProviderKind { get; set; }

    public bool IsEnabled { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
