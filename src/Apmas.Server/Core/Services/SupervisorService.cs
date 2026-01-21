using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Background service that monitors agent health, processes pending agents, and manages agent lifecycle.
/// </summary>
public class SupervisorService : BackgroundService
{
    private readonly IAgentStateManager _stateManager;
    private readonly IAgentSpawner _agentSpawner;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<SupervisorService> _logger;
    private readonly ApmasOptions _options;

    public SupervisorService(
        IAgentStateManager stateManager,
        IAgentSpawner agentSpawner,
        IMessageBus messageBus,
        ILogger<SupervisorService> logger,
        IOptions<ApmasOptions> options)
    {
        _stateManager = stateManager;
        _agentSpawner = agentSpawner;
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SupervisorService started");

        // Wait a short delay on startup to allow initialization
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAgentHealthAsync(stoppingToken);
                await CheckDependenciesAsync(stoppingToken);
                await ProcessQueuedAgentsAsync(stoppingToken);

                // Polling interval from configuration
                await Task.Delay(TimeSpan.FromSeconds(_options.Timeouts.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in supervisor polling loop");
                // Continue running despite errors
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("SupervisorService stopping");
    }

    /// <summary>
    /// Checks the health of all active agents by monitoring their heartbeats.
    /// </summary>
    private async Task CheckAgentHealthAsync(CancellationToken cancellationToken)
    {
        var activeAgents = await _stateManager.GetActiveAgentsAsync();
        var now = DateTime.UtcNow;
        var heartbeatTimeout = _options.Timeouts.HeartbeatTimeout;

        foreach (var agent in activeAgents)
        {
            // Skip agents that haven't spawned yet
            if (agent.Status != AgentStatus.Running)
            {
                continue;
            }

            // If no heartbeat has been received yet, use SpawnedAt as the baseline
            var lastHeartbeat = agent.LastHeartbeat ?? agent.SpawnedAt;

            if (lastHeartbeat == null)
            {
                _logger.LogWarning("Agent {AgentRole} is running but has no heartbeat or spawn timestamp", agent.Role);
                continue;
            }

            var timeSinceHeartbeat = now - lastHeartbeat.Value;

            if (timeSinceHeartbeat > heartbeatTimeout)
            {
                _logger.LogWarning(
                    "Agent {AgentRole} has not sent heartbeat in {ElapsedMinutes:F1} minutes (timeout: {TimeoutMinutes} minutes)",
                    agent.Role,
                    timeSinceHeartbeat.TotalMinutes,
                    heartbeatTimeout.TotalMinutes);

                await HandleUnhealthyAgentAsync(agent, cancellationToken);
            }
            else
            {
                _logger.LogDebug(
                    "Agent {AgentRole} is healthy (last heartbeat {ElapsedSeconds:F0} seconds ago)",
                    agent.Role,
                    timeSinceHeartbeat.TotalSeconds);
            }
        }
    }

    /// <summary>
    /// Handles an unhealthy agent by retrying or escalating.
    /// </summary>
    private async Task HandleUnhealthyAgentAsync(AgentState agent, CancellationToken cancellationToken)
    {
        var maxRetries = _options.Timeouts.MaxRetries;

        if (agent.RetryCount >= maxRetries)
        {
            _logger.LogError(
                "Agent {AgentRole} has exceeded max retries ({RetryCount}/{MaxRetries}), escalating to human",
                agent.Role,
                agent.RetryCount,
                maxRetries);

            await _stateManager.UpdateAgentStateAsync(agent.Role, a =>
            {
                a.Status = AgentStatus.Escalated;
                a.LastError = $"Agent timed out after {agent.RetryCount} retries";
                return a;
            });
        }
        else
        {
            _logger.LogWarning(
                "Agent {AgentRole} timed out, marking for retry (attempt {RetryCount}/{MaxRetries})",
                agent.Role,
                agent.RetryCount + 1,
                maxRetries);

            await _stateManager.UpdateAgentStateAsync(agent.Role, a =>
            {
                a.Status = AgentStatus.TimedOut;
                a.RetryCount = a.RetryCount + 1;
                a.LastError = "Heartbeat timeout";
                return a;
            });

            // TODO: In a future implementation, this would trigger a restart with checkpoint context
            // For now, we just mark it as timed out for manual intervention
        }
    }

    /// <summary>
    /// Processes queued agents and spawns them.
    /// </summary>
    private async Task ProcessQueuedAgentsAsync(CancellationToken cancellationToken)
    {
        var readyAgents = await _stateManager.GetReadyAgentsAsync();

        foreach (var agentRole in readyAgents)
        {
            try
            {
                // Get the current agent state to check if it's Queued
                var currentAgent = await _stateManager.GetAgentStateAsync(agentRole);

                // Only spawn agents that are Queued (not Pending)
                if (currentAgent.Status != AgentStatus.Queued)
                {
                    continue;
                }

                // Atomically capture agent state and update status to Spawning
                AgentState? agentState = null;
                await _stateManager.UpdateAgentStateAsync(agentRole, a =>
                {
                    agentState = a;
                    a.Status = AgentStatus.Spawning;
                    return a;
                });

                if (agentState == null)
                {
                    _logger.LogError("Failed to retrieve agent state for {AgentRole}", agentRole);
                    continue;
                }

                _logger.LogInformation("Spawning agent {AgentRole} with subagent type {SubagentType}",
                    agentState.Role,
                    agentState.SubagentType);

                // Spawn the agent
                var taskId = await _agentSpawner.SpawnAgentAsync(
                    agentState.Role,
                    agentState.SubagentType,
                    checkpointContext: null);

                // Update status to Running with spawn timestamp
                await _stateManager.UpdateAgentStateAsync(agentState.Role, a =>
                {
                    a.Status = AgentStatus.Running;
                    a.TaskId = taskId;
                    a.SpawnedAt = DateTime.UtcNow;
                    a.TimeoutAt = DateTime.UtcNow.Add(_options.Timeouts.GetTimeoutFor(agentState.Role));
                    return a;
                });

                _logger.LogInformation(
                    "Agent {AgentRole} spawned successfully with task ID {TaskId}",
                    agentState.Role,
                    taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn agent {AgentRole}", agentRole);

                await _stateManager.UpdateAgentStateAsync(agentRole, a =>
                {
                    a.Status = AgentStatus.Failed;
                    a.LastError = $"Spawn failed: {ex.Message}";
                    return a;
                });
            }
        }
    }

    /// <summary>
    /// Checks dependencies for all pending agents and updates their queue status.
    /// </summary>
    private async Task CheckDependenciesAsync(CancellationToken cancellationToken)
    {
        var readyAgents = await _stateManager.GetReadyAgentsAsync();

        foreach (var agentRole in readyAgents)
        {
            var agent = await _stateManager.GetAgentStateAsync(agentRole);
            if (agent.Status == AgentStatus.Pending)
            {
                await _stateManager.UpdateAgentStateAsync(agentRole, a =>
                {
                    a.Status = AgentStatus.Queued;
                    return a;
                });
                _logger.LogInformation("Agent {AgentRole} queued (dependencies met)", agentRole);
            }
        }
    }
}
