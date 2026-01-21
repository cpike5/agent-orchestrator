using System.Collections.Concurrent;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Implementation of IHeartbeatMonitor that tracks agent liveness using thread-safe in-memory storage.
/// </summary>
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, HeartbeatInfo> _heartbeats = new();
    private readonly IAgentStateManager _agentStateManager;
    private readonly ILogger<HeartbeatMonitor> _logger;
    private readonly ApmasOptions _options;

    /// <summary>
    /// Represents heartbeat information for a single agent.
    /// </summary>
    /// <param name="Timestamp">When the heartbeat was received.</param>
    /// <param name="Status">Current activity status (working, thinking, writing).</param>
    /// <param name="Progress">Optional progress message.</param>
    private record HeartbeatInfo(DateTime Timestamp, string Status, string? Progress);

    public HeartbeatMonitor(
        IAgentStateManager agentStateManager,
        ILogger<HeartbeatMonitor> logger,
        IOptions<ApmasOptions> options)
    {
        _agentStateManager = agentStateManager;
        _logger = logger;
        _options = options.Value;
    }

    public void RecordHeartbeat(string agentRole, string status, string? progress)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));

        var heartbeatInfo = new HeartbeatInfo(DateTime.UtcNow, status, progress);

        _heartbeats.AddOrUpdate(
            agentRole,
            heartbeatInfo,
            (_, _) => heartbeatInfo);

        _logger.LogDebug(
            "Recorded heartbeat for agent {AgentRole} with status {Status}",
            agentRole,
            status);
    }

    public async Task<bool> IsAgentHealthyAsync(string agentRole)
    {
        var heartbeatTimeout = _options.Timeouts.HeartbeatTimeout;
        var now = DateTime.UtcNow;

        // Check in-memory heartbeat first
        if (_heartbeats.TryGetValue(agentRole, out var heartbeatInfo))
        {
            var timeSinceHeartbeat = now - heartbeatInfo.Timestamp;
            return timeSinceHeartbeat <= heartbeatTimeout;
        }

        // Fall back to checking AgentState if no in-memory heartbeat exists
        try
        {
            var agent = await _agentStateManager.GetAgentStateAsync(agentRole);

            // Non-Running agents are not being monitored, so they're considered "healthy"
            if (agent.Status != AgentStatus.Running)
            {
                return true;
            }

            // Use LastHeartbeat or SpawnedAt as baseline
            var lastHeartbeat = agent.LastHeartbeat ?? agent.SpawnedAt;
            if (lastHeartbeat == null)
            {
                // Agent is Running but has no timestamp - consider unhealthy
                return false;
            }

            var timeSinceLastActivity = now - lastHeartbeat.Value;
            return timeSinceLastActivity <= heartbeatTimeout;
        }
        catch (KeyNotFoundException)
        {
            // Agent doesn't exist - not healthy
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetUnhealthyAgentsAsync()
    {
        // Get all active agents from state manager
        var activeAgents = await _agentStateManager.GetActiveAgentsAsync();

        // Filter to only Running agents
        var runningAgents = activeAgents.Where(a => a.Status == AgentStatus.Running).ToList();

        var unhealthyAgents = new List<string>();
        var heartbeatTimeout = _options.Timeouts.HeartbeatTimeout;
        var now = DateTime.UtcNow;

        foreach (var agent in runningAgents)
        {
            // Check if agent has a recorded heartbeat
            if (_heartbeats.TryGetValue(agent.Role, out var heartbeatInfo))
            {
                var timeSinceHeartbeat = now - heartbeatInfo.Timestamp;

                if (timeSinceHeartbeat > heartbeatTimeout)
                {
                    unhealthyAgents.Add(agent.Role);

                    _logger.LogInformation(
                        "Agent {AgentRole} is unhealthy (last heartbeat {Minutes:F1} minutes ago)",
                        agent.Role,
                        timeSinceHeartbeat.TotalMinutes);
                }
            }
            else
            {
                // No heartbeat recorded yet - use SpawnedAt as baseline
                var lastHeartbeat = agent.LastHeartbeat ?? agent.SpawnedAt;

                if (lastHeartbeat == null)
                {
                    _logger.LogDebug(
                        "Agent {AgentRole} is running but has no heartbeat or spawn timestamp",
                        agent.Role);
                    continue;
                }

                var timeSinceSpawn = now - lastHeartbeat.Value;

                if (timeSinceSpawn > heartbeatTimeout)
                {
                    unhealthyAgents.Add(agent.Role);

                    _logger.LogInformation(
                        "Agent {AgentRole} is unhealthy (no heartbeat, spawned {Minutes:F1} minutes ago)",
                        agent.Role,
                        timeSinceSpawn.TotalMinutes);
                }
            }
        }

        return unhealthyAgents;
    }

    public void ClearAgent(string agentRole)
    {
        if (_heartbeats.TryRemove(agentRole, out _))
        {
            _logger.LogDebug("Cleared heartbeat tracking for agent {AgentRole}", agentRole);
        }
    }
}
