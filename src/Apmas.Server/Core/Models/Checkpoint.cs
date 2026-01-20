namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a checkpoint of agent progress for context recovery.
/// </summary>
public record Checkpoint
{
    /// <summary>
    /// Unique identifier for the checkpoint.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Role of the agent that created this checkpoint.
    /// </summary>
    public required string AgentRole { get; init; }

    /// <summary>
    /// When the checkpoint was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Brief summary of the current state.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Progress information for this checkpoint.
    /// </summary>
    public required CheckpointProgress Progress { get; init; }

    /// <summary>
    /// List of completed task items.
    /// </summary>
    public required IReadOnlyList<string> CompletedItems { get; init; }

    /// <summary>
    /// List of pending task items.
    /// </summary>
    public required IReadOnlyList<string> PendingItems { get; init; }

    /// <summary>
    /// List of files currently being worked on.
    /// </summary>
    public required IReadOnlyList<string> ActiveFiles { get; init; }

    /// <summary>
    /// Optional notes for continuation.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Estimated context token usage at checkpoint time.
    /// </summary>
    public int? EstimatedContextUsage { get; init; }
}

/// <summary>
/// Represents progress information within a checkpoint.
/// </summary>
public record CheckpointProgress
{
    /// <summary>
    /// Number of completed tasks.
    /// </summary>
    public int CompletedTasks { get; init; }

    /// <summary>
    /// Total number of tasks.
    /// </summary>
    public int TotalTasks { get; init; }

    /// <summary>
    /// Percentage of tasks completed.
    /// </summary>
    public double PercentComplete => TotalTasks > 0
        ? (double)CompletedTasks / TotalTasks * 100
        : 0;
}
