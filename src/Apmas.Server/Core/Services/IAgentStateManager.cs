using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Manages agent and project state with caching and state transitions.
/// </summary>
public interface IAgentStateManager
{
    /// <summary>
    /// Gets the current project state.
    /// </summary>
    Task<ProjectState> GetProjectStateAsync();

    /// <summary>
    /// Gets the state of a specific agent by role.
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    Task<AgentState> GetAgentStateAsync(string agentRole);

    /// <summary>
    /// Updates the state of a specific agent.
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    /// <param name="state">The new state to save.</param>
    Task UpdateAgentStateAsync(string agentRole, AgentState state);

    /// <summary>
    /// Updates the state of a specific agent using a transformation function.
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    /// <param name="update">Function to transform the current state to the new state.</param>
    Task UpdateAgentStateAsync(string agentRole, Func<AgentState, AgentState> update);

    /// <summary>
    /// Gets all agents currently in active states (Running, Spawning, or Paused).
    /// </summary>
    Task<IReadOnlyList<AgentState>> GetActiveAgentsAsync();

    /// <summary>
    /// Gets all agents that are ready to be spawned (dependencies completed, status is Pending or Queued).
    /// </summary>
    Task<IReadOnlyList<string>> GetReadyAgentsAsync();

    /// <summary>
    /// Initializes a new project with the given name and working directory.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <param name="workingDirectory">The working directory for the project.</param>
    Task InitializeProjectAsync(string name, string workingDirectory);

    /// <summary>
    /// Initializes project and agents from configuration if not already initialized.
    /// Creates the project state and agent states in Pending status from the roster.
    /// </summary>
    /// <returns>True if initialization was performed, false if project already existed.</returns>
    Task<bool> InitializeFromConfigAsync();
}
