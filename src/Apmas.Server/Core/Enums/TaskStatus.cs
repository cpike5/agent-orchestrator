namespace Apmas.Server.Core.Enums;

/// <summary>
/// Status of a task in the task queue.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is waiting to be executed.</summary>
    Pending,

    /// <summary>Task is currently being worked on by an agent.</summary>
    InProgress,

    /// <summary>Task completed successfully.</summary>
    Completed,

    /// <summary>Task failed after retries exhausted.</summary>
    Failed,

    /// <summary>Task is blocked waiting on external input.</summary>
    Blocked,

    /// <summary>Task was skipped (marked unnecessary).</summary>
    Skipped
}
