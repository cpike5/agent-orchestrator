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
}
