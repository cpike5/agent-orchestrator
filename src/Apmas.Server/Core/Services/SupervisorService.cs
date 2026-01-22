using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Mcp.Http;
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
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ITimeoutHandler _timeoutHandler;
    private readonly IHttpServerReadySignal _httpServerReadySignal;
    private readonly IApmasMetrics _metrics;
    private readonly ILogger<SupervisorService> _logger;
    private readonly ApmasOptions _options;

    public SupervisorService(
        IAgentStateManager stateManager,
        IAgentSpawner agentSpawner,
        IMessageBus messageBus,
        IHeartbeatMonitor heartbeatMonitor,
        ITimeoutHandler timeoutHandler,
        IHttpServerReadySignal httpServerReadySignal,
        IApmasMetrics metrics,
        ILogger<SupervisorService> logger,
        IOptions<ApmasOptions> options)
    {
        _stateManager = stateManager;
        _agentSpawner = agentSpawner;
        _messageBus = messageBus;
        _heartbeatMonitor = heartbeatMonitor;
        _timeoutHandler = timeoutHandler;
        _httpServerReadySignal = httpServerReadySignal;
        _metrics = metrics;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SupervisorService started");

        // Wait for HTTP MCP server to be ready before spawning agents
        // This ensures agents can connect to the HTTP endpoint
        _logger.LogInformation("Waiting for HTTP MCP server to be ready...");
        var httpReady = await _httpServerReadySignal.WaitForReadyAsync(
            TimeSpan.FromSeconds(30),
            stoppingToken);

        if (!httpReady)
        {
            _logger.LogWarning("HTTP MCP server did not become ready within timeout. Agents may fail to connect.");
        }
        else
        {
            _logger.LogInformation("HTTP MCP server is ready. Proceeding with agent management.");
        }

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
        var unhealthyAgentRoles = await _heartbeatMonitor.GetUnhealthyAgentsAsync();

        foreach (var agentRole in unhealthyAgentRoles)
        {
            var agent = await _stateManager.GetAgentStateAsync(agentRole);

            _logger.LogWarning(
                "Agent {AgentRole} has not sent heartbeat within timeout threshold",
                agentRole);

            await HandleUnhealthyAgentAsync(agent, cancellationToken);
        }
    }

    /// <summary>
    /// Handles an unhealthy agent by delegating to the TimeoutHandler.
    /// </summary>
    private async Task HandleUnhealthyAgentAsync(AgentState agent, CancellationToken cancellationToken)
    {
        _metrics.RecordAgentTimedOut(agent.Role);
        await _timeoutHandler.HandleTimeoutAsync(agent.Role, cancellationToken);
        await _metrics.UpdateCachedMetricsAsync();
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
                string? recoveryContext = null;
                await _stateManager.UpdateAgentStateAsync(agentRole, a =>
                {
                    agentState = a;
                    recoveryContext = a.RecoveryContext;
                    a.Status = AgentStatus.Spawning;
                    // Clear recovery context after capturing it
                    a.RecoveryContext = null;
                    return a;
                });

                if (agentState == null)
                {
                    _logger.LogError("Failed to retrieve agent state for {AgentRole}", agentRole);
                    continue;
                }

                if (recoveryContext != null)
                {
                    _logger.LogInformation(
                        "Spawning agent {AgentRole} with recovery context (retry {RetryCount})",
                        agentState.Role,
                        agentState.RetryCount);
                }
                else
                {
                    _logger.LogInformation("Spawning agent {AgentRole} with subagent type {SubagentType}",
                        agentState.Role,
                        agentState.SubagentType);
                }

                // Spawn the agent with recovery context if available
                var spawnResult = await _agentSpawner.SpawnAgentAsync(
                    agentState.Role,
                    agentState.SubagentType,
                    checkpointContext: recoveryContext);

                // Check if spawn was successful
                if (!spawnResult.Success)
                {
                    _logger.LogError(
                        "Failed to spawn agent {AgentRole}: {ErrorMessage}",
                        agentState.Role,
                        spawnResult.ErrorMessage);

                    _metrics.RecordAgentFailed(agentState.Role, spawnResult.ErrorMessage ?? "Unknown spawn error");

                    await _stateManager.UpdateAgentStateAsync(agentState.Role, a =>
                    {
                        a.Status = AgentStatus.Failed;
                        a.LastError = spawnResult.ErrorMessage;
                        a.RetryCount++;
                        return a;
                    });

                    await _metrics.UpdateCachedMetricsAsync();

                    continue;
                }

                // Update status to Running with spawn timestamp
                await _stateManager.UpdateAgentStateAsync(agentState.Role, a =>
                {
                    a.Status = AgentStatus.Running;
                    a.TaskId = spawnResult.TaskId;
                    a.SpawnedAt = DateTime.UtcNow;
                    a.TimeoutAt = DateTime.UtcNow.Add(_options.Timeouts.GetTimeoutFor(agentState.Role));
                    return a;
                });

                _metrics.RecordAgentSpawned(agentState.Role);
                await _metrics.UpdateCachedMetricsAsync();

                _logger.LogInformation(
                    "Agent {AgentRole} spawned successfully with task ID {TaskId} and PID {ProcessId}",
                    agentState.Role,
                    spawnResult.TaskId,
                    spawnResult.ProcessId);
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
