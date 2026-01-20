namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a checkpoint of an agent's progress.
/// </summary>
public class Checkpoint
{
    /// <summary>
    /// Primary key for EF Core.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Role of the agent that created this checkpoint.
    /// </summary>
    public required string AgentRole { get; set; }

    /// <summary>
    /// When the checkpoint was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Brief summary of current state.
    /// </summary>
    public required string Summary { get; set; }

    /// <summary>
    /// Number of completed tasks.
    /// </summary>
    public int CompletedTaskCount { get; set; }

    /// <summary>
    /// Total number of tasks.
    /// </summary>
    public int TotalTaskCount { get; set; }

    /// <summary>
    /// Percentage of tasks completed.
    /// </summary>
    public double PercentComplete => TotalTaskCount > 0
        ? (double)CompletedTaskCount / TotalTaskCount * 100
        : 0;

    /// <summary>
    /// JSON-serialized list of completed items.
    /// </summary>
    public string? CompletedItemsJson { get; set; }

    /// <summary>
    /// JSON-serialized list of pending items.
    /// </summary>
    public string? PendingItemsJson { get; set; }

    /// <summary>
    /// JSON-serialized list of active file paths.
    /// </summary>
    public string? ActiveFilesJson { get; set; }

    /// <summary>
    /// Additional notes for continuation.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Estimated context usage in tokens.
    /// </summary>
    public int? EstimatedContextUsage { get; set; }
}
