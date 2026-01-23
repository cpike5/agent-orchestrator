namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a task in the task queue for sequential execution by developer agents.
/// </summary>
public class TaskItem
{
    /// <summary>
    /// Database primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique task identifier (e.g., "task-001").
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Parent task ID for subtask relationships.
    /// </summary>
    public string? ParentTaskId { get; init; }

    /// <summary>
    /// Execution order (lower numbers execute first).
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Brief task title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Detailed task description with instructions.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// JSON array of file paths this task will create/modify.
    /// </summary>
    public string? FilesJson { get; set; }

    /// <summary>
    /// Optional phase grouping for review checkpoints.
    /// </summary>
    public string? Phase { get; set; }

    /// <summary>
    /// Current task status.
    /// </summary>
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Pending;

    /// <summary>
    /// Role of the agent assigned to this task (e.g., "Developer-task-001").
    /// </summary>
    public string? AssignedAgentRole { get; set; }

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When work started on the task.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the task was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Summary of the result from the agent.
    /// </summary>
    public string? ResultSummary { get; set; }

    /// <summary>
    /// JSON array of artifact file paths produced.
    /// </summary>
    public string? ArtifactsJson { get; set; }

    /// <summary>
    /// Error message if task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }
}
