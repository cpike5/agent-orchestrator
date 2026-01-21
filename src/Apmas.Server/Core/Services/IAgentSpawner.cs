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
    /// <returns>The task ID of the spawned agent.</returns>
    /// <exception cref="InvalidOperationException">Thrown when agent spawning fails.</exception>
    Task<string> SpawnAgentAsync(string agentRole, string subagentType, string? checkpointContext = null);
}
