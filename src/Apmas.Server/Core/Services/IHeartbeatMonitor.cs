namespace Apmas.Server.Core.Services;

/// <summary>
/// Monitors agent heartbeats to track liveness and detect unhealthy agents.
/// </summary>
public interface IHeartbeatMonitor
{
    /// <summary>
    /// Records a heartbeat from an agent.
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    /// <param name="status">Current activity status (working, thinking, writing).</param>
    /// <param name="progress">Optional progress message.</param>
    void RecordHeartbeat(string agentRole, string status, string? progress);

    /// <summary>
    /// Checks if an agent is healthy based on its heartbeat status.
    /// Returns true if the agent has sent a heartbeat within the timeout threshold
    /// or if the agent has not sent any heartbeat yet (newly spawned).
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    /// <returns>True if the agent is healthy, false if it has exceeded the timeout.</returns>
    Task<bool> IsAgentHealthyAsync(string agentRole);

    /// <summary>
    /// Gets a list of all unhealthy agents that are currently running
    /// but have exceeded the heartbeat timeout threshold.
    /// </summary>
    /// <returns>List of agent role identifiers that are unhealthy.</returns>
    Task<IReadOnlyList<string>> GetUnhealthyAgentsAsync();

    /// <summary>
    /// Removes an agent from the heartbeat tracking dictionary.
    /// Should be called when an agent is stopped or removed.
    /// </summary>
    /// <param name="agentRole">The role identifier of the agent.</param>
    void ClearAgent(string agentRole);
}
