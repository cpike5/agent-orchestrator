using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for managing the task execution queue.
/// </summary>
public interface ITaskQueueService
{
    /// <summary>
    /// Checks if there are pending tasks in the queue.
    /// </summary>
    Task<bool> HasPendingTasksAsync();

    /// <summary>
    /// Dequeues the next pending task and marks it as in-progress.
    /// Returns null if no pending tasks.
    /// </summary>
    Task<TaskItem?> DequeueNextTaskAsync();

    /// <summary>
    /// Submits a batch of tasks to the queue.
    /// </summary>
    Task SubmitTasksAsync(IEnumerable<TaskItem> tasks);

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    Task UpdateTaskStatusAsync(string taskId, Enums.TaskStatus status, string? summary = null, string? error = null);

    /// <summary>
    /// Gets the task currently being executed (if any).
    /// </summary>
    Task<TaskItem?> GetCurrentTaskAsync();

    /// <summary>
    /// Gets the status of all tasks in the queue.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetTaskQueueStatusAsync();

    /// <summary>
    /// Checks if all tasks in a phase have completed.
    /// </summary>
    Task<bool> IsPhaseCompleteAsync(string phase);

    /// <summary>
    /// Checks if all tasks have been completed.
    /// </summary>
    Task<bool> AreAllTasksCompleteAsync();
}
