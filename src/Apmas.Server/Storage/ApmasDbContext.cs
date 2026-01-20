using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace Apmas.Server.Storage;

/// <summary>
/// EF Core database context for APMAS state persistence.
/// </summary>
public class ApmasDbContext : DbContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        base.OnModelCreating(modelBuilder);

        ConfigureProjectState(modelBuilder);
        ConfigureAgentState(modelBuilder);
        ConfigureAgentMessage(modelBuilder);
        ConfigureCheckpoint(modelBuilder);
    }

    private static void ConfigureProjectState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProjectState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.WorkingDirectory).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Phase).HasConversion<string>();
            entity.Property(e => e.StartedAt).IsRequired();

            // Ignore the Agents dictionary - we use AgentStates table directly
            entity.Ignore(e => e.Agents);
        });
    }

    private static void ConfigureAgentState(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>());

        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<AgentState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.SubagentType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TaskId).HasMaxLength(200);
            entity.Property(e => e.LastMessage).HasMaxLength(2000);
            entity.Property(e => e.LastError).HasMaxLength(2000);

            // JSON columns for complex types
            entity.Property(e => e.Artifacts)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(e => e.Dependencies)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            // Indexes
            entity.HasIndex(e => e.Role);
            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureAgentMessage(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<IReadOnlyList<string>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
            v => v == null ? null : JsonSerializer.Deserialize<List<string>>(v, JsonOptions));

        var stringListComparer = new ValueComparer<IReadOnlyList<string>?>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? null : c.ToList());

        var metadataConverter = new ValueConverter<IReadOnlyDictionary<string, object>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
            v => v == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonOptions));

        var metadataComparer = new ValueComparer<IReadOnlyDictionary<string, object>?>(
            (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
            c => c == null ? 0 : JsonSerializer.Serialize(c, JsonOptions).GetHashCode(),
            c => c == null ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(
                JsonSerializer.Serialize(c, JsonOptions), JsonOptions));

        modelBuilder.Entity<AgentMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.From).IsRequired().HasMaxLength(100);
            entity.Property(e => e.To).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Content).IsRequired();

            // JSON columns for complex types
            entity.Property(e => e.Artifacts)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(e => e.Metadata)
                .HasConversion(metadataConverter)
                .Metadata.SetValueComparer(metadataComparer);

            // Indexes
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.From);
            entity.HasIndex(e => e.To);
        });
    }

    private static void ConfigureCheckpoint(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<IReadOnlyList<string>, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>());

        var stringListComparer = new ValueComparer<IReadOnlyList<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        var progressConverter = new ValueConverter<CheckpointProgress, string>(
            v => JsonSerializer.Serialize(v, JsonOptions),
            v => JsonSerializer.Deserialize<CheckpointProgress>(v, JsonOptions) ?? new CheckpointProgress());

        var progressComparer = new ValueComparer<CheckpointProgress>(
            (c1, c2) => c1!.CompletedTasks == c2!.CompletedTasks && c1.TotalTasks == c2.TotalTasks,
            c => HashCode.Combine(c.CompletedTasks, c.TotalTasks),
            c => new CheckpointProgress { CompletedTasks = c.CompletedTasks, TotalTasks = c.TotalTasks });

        modelBuilder.Entity<Checkpoint>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AgentRole).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.Summary).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Notes).HasMaxLength(2000);

            // JSON columns for complex types
            entity.Property(e => e.Progress)
                .HasConversion(progressConverter)
                .Metadata.SetValueComparer(progressComparer);

            entity.Property(e => e.CompletedItems)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(e => e.PendingItems)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(e => e.ActiveFiles)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            // Indexes
            entity.HasIndex(e => e.AgentRole);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
