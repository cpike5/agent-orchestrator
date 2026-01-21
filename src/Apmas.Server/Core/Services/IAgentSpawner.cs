using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Responsible for spawning Claude Code agents via CLI.
/// </summary>
public interface IAgentSpawner
{
    /// <summary>
    /// Spawns a new agent with the specified role and subagent type.
    /// </summary>
    /// <param name="agentRole">The role identifier for the agent (e.g., "architect").</param>
    /// <param name="subagentType">The subagent type to spawn (e.g., "systems-architect").</param>
    /// <param name="checkpointContext">Optional checkpoint context for recovery.</param>
    /// <returns>A SpawnResult containing the task ID, process ID, and success status.</returns>
    Task<SpawnResult> SpawnAgentAsync(string agentRole, string subagentType, string? checkpointContext = null);

    /// <summary>
    /// Terminates a running agent process.
    /// </summary>
    /// <param name="agentRole">The role identifier for the agent to terminate.</param>
    /// <returns>True if the agent was successfully terminated, false otherwise.</returns>
    Task<bool> TerminateAgentAsync(string agentRole);

    /// <summary>
    /// Gets information about a running or terminated agent process.
    /// </summary>
    /// <param name="agentRole">The role identifier for the agent.</param>
    /// <returns>Process information if the agent exists, null otherwise.</returns>
    Task<AgentProcessInfo?> GetAgentProcessAsync(string agentRole);
}
