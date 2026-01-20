using Apmas.Server.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Apmas.Server.Storage;

/// <summary>
/// EF Core database context for APMAS state persistence.
/// </summary>
public class ApmasDbContext : DbContext
{
    public ApmasDbContext(DbContextOptions<ApmasDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectState> ProjectStates => Set<ProjectState>();
    public DbSet<AgentState> AgentStates => Set<AgentState>();
    public DbSet<AgentMessage> Messages => Set<AgentMessage>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.WorkingDirectory).IsRequired();
        });

        modelBuilder.Entity<AgentState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired();
            entity.Property(e => e.SubagentType).IsRequired();
            entity.HasIndex(e => e.Role).IsUnique();
        });

        modelBuilder.Entity<AgentMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.From).IsRequired();
            entity.Property(e => e.To).IsRequired();
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.From);
            entity.HasIndex(e => e.To);
        });

        modelBuilder.Entity<Checkpoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentRole).IsRequired();
            entity.Property(e => e.Summary).IsRequired();
            entity.HasIndex(e => new { e.AgentRole, e.CreatedAt });
            entity.Ignore(e => e.PercentComplete);
        });
    }
}
