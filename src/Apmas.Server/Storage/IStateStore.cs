using Apmas.Server.Core.Models;

namespace Apmas.Server.Storage;

/// <summary>
/// Storage abstraction for APMAS state persistence.
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Gets the current project state.
    /// </summary>
    Task<ProjectState?> GetProjectStateAsync();

    /// <summary>
    /// Saves the project state.
    /// </summary>
    Task SaveProjectStateAsync(ProjectState state);

    /// <summary>
    /// Gets the state of a specific agent by role.
    /// </summary>
    Task<AgentState?> GetAgentStateAsync(string role);

    /// <summary>
    /// Saves an agent state.
    /// </summary>
    Task SaveAgentStateAsync(AgentState state);

    /// <summary>
    /// Gets all agent states.
    /// </summary>
    Task<IReadOnlyList<AgentState>> GetAllAgentStatesAsync();

    /// <summary>
    /// Gets the latest checkpoint for an agent.
    /// </summary>
    Task<Checkpoint?> GetLatestCheckpointAsync(string role);

    /// <summary>
    /// Saves a checkpoint.
    /// </summary>
    Task SaveCheckpointAsync(Checkpoint checkpoint);

    /// <summary>
    /// Gets the checkpoint history for an agent, ordered by creation time descending.
    /// </summary>
    /// <param name="role">The agent role to get checkpoints for.</param>
    /// <param name="limit">Maximum number of checkpoints to return, or null for all.</param>
    Task<IReadOnlyList<Checkpoint>> GetCheckpointHistoryAsync(string role, int? limit = null);

    /// <summary>
    /// Gets messages, optionally filtered by role and time.
    /// </summary>
    /// <param name="role">Filter to messages to/from this role, or null for all.</param>
    /// <param name="since">Filter to messages after this time, or null for all.</param>
    /// <param name="limit">Maximum number of messages to return, or null for all.</param>
    Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(string? role = null, DateTime? since = null, int? limit = null);

    /// <summary>
    /// Saves a message.
    /// </summary>
    Task SaveMessageAsync(AgentMessage message);

    /// <summary>
    /// Gets a task by its unique task ID.
    /// </summary>
    Task<TaskItem?> GetTaskAsync(string taskId);

    /// <summary>
    /// Saves a task (insert or update).
    /// </summary>
    Task SaveTaskAsync(TaskItem task);

    /// <summary>
    /// Gets all tasks with the specified status.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetTasksByStatusAsync(Core.Enums.TaskStatus status);

    /// <summary>
    /// Gets the next pending task in sequence order.
    /// </summary>
    Task<TaskItem?> GetNextPendingTaskAsync();

    /// <summary>
    /// Gets all tasks ordered by sequence number.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> GetAllTasksAsync();

    /// <summary>
    /// Checks if all tasks in a phase are completed.
    /// </summary>
    Task<bool> AreAllTasksInPhaseCompletedAsync(string phase);
}
