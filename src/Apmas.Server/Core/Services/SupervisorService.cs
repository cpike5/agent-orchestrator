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
    private readonly IDashboardEventPublisher _dashboardEvents;
    private readonly ITaskQueueService _taskQueue;
    private readonly ILogger<SupervisorService> _logger;
    private readonly ApmasOptions _options;
    private readonly TaskOrchestrationOptions _taskOptions;

    public SupervisorService(
        IAgentStateManager stateManager,
        IAgentSpawner agentSpawner,
        IMessageBus messageBus,
        IHeartbeatMonitor heartbeatMonitor,
        ITimeoutHandler timeoutHandler,
        IHttpServerReadySignal httpServerReadySignal,
        IApmasMetrics metrics,
        IDashboardEventPublisher dashboardEvents,
        ITaskQueueService taskQueue,
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
        _dashboardEvents = dashboardEvents;
        _taskQueue = taskQueue;
        _logger = logger;
        _options = options.Value;
        _taskOptions = options.Value.TaskOrchestration;
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

                if (_taskOptions.Enabled)
                {
                    await ProcessTaskQueueAsync(stoppingToken);
                }
                else
                {
                    await ProcessQueuedAgentsAsync(stoppingToken);
                }

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

                // Publish agent update after Queued→Spawning transition
                try
                {
                    await _dashboardEvents.PublishAgentUpdateAsync(agentState);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agentRole);
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

                    // Publish agent update after spawn failure
                    var failedAgent = await _stateManager.GetAgentStateAsync(agentState.Role);
                    try
                    {
                        await _dashboardEvents.PublishAgentUpdateAsync(failedAgent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agentState.Role);
                    }

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

                // Publish agent update after successful spawn (Spawning→Running)
                var runningAgent = await _stateManager.GetAgentStateAsync(agentState.Role);
                try
                {
                    await _dashboardEvents.PublishAgentUpdateAsync(runningAgent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agentState.Role);
                }
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

                // Publish agent update after exception failure
                var failedAgent = await _stateManager.GetAgentStateAsync(agentRole);
                try
                {
                    await _dashboardEvents.PublishAgentUpdateAsync(failedAgent);
                }
                catch (Exception publishEx)
                {
                    _logger.LogWarning(publishEx, "Failed to publish dashboard event for agent {AgentRole}", agentRole);
                }
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

                // Publish agent update after Pending→Queued transition
                var updatedAgent = await _stateManager.GetAgentStateAsync(agentRole);
                try
                {
                    await _dashboardEvents.PublishAgentUpdateAsync(updatedAgent);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agentRole);
                }
            }
        }
    }

    /// <summary>
    /// Processes the task queue when task-based orchestration is enabled.
    /// </summary>
    private async Task ProcessTaskQueueAsync(CancellationToken ct)
    {
        // 1. First, check if architect has completed (and submitted tasks)
        var architect = await _stateManager.GetAgentStateAsync("architect");
        if (architect == null || architect.Status != AgentStatus.Completed)
        {
            // Still waiting on architect - use normal agent processing for now
            await ProcessQueuedAgentsAsync(ct);
            return;
        }

        // 2. Check if we have a task in progress
        var currentTask = await _taskQueue.GetCurrentTaskAsync();
        if (currentTask != null)
        {
            // Check if the agent assigned to this task has completed
            var taskAgent = await _stateManager.GetAgentStateAsync(currentTask.AssignedAgentRole!);
            if (taskAgent == null)
            {
                // Agent doesn't exist yet - this shouldn't happen
                _logger.LogWarning("Task {TaskId} has assigned agent {Agent} but agent state not found",
                    currentTask.TaskId, currentTask.AssignedAgentRole);
                return;
            }

            if (taskAgent.Status == AgentStatus.Completed)
            {
                _logger.LogInformation("Task {TaskId} completed by {Agent}", currentTask.TaskId, taskAgent.Role);
                await _taskQueue.UpdateTaskStatusAsync(currentTask.TaskId, Enums.TaskStatus.Completed, taskAgent.LastMessage);

                // Publish dashboard event
                await _dashboardEvents.PublishAgentUpdateAsync(taskAgent);
            }
            else if (taskAgent.Status == AgentStatus.Failed || taskAgent.Status == AgentStatus.Escalated)
            {
                _logger.LogWarning("Task {TaskId} failed: {Error}", currentTask.TaskId, taskAgent.LastError);
                await _taskQueue.UpdateTaskStatusAsync(currentTask.TaskId, Enums.TaskStatus.Failed, error: taskAgent.LastError);
                // TODO: Implement retry logic or escalation
                return;
            }
            else
            {
                // Task still in progress
                return;
            }
        }

        // 3. Check if all tasks are complete
        if (await _taskQueue.AreAllTasksCompleteAsync())
        {
            _logger.LogInformation("All tasks completed - triggering final review");
            // Transition to review phase if needed
            // For now, just spawn the reviewer using normal dependency resolution
            await ProcessQueuedAgentsAsync(ct);
            return;
        }

        // 4. Dequeue next task
        var nextTask = await _taskQueue.DequeueNextTaskAsync();
        if (nextTask == null)
        {
            // No pending tasks - might be waiting for something
            return;
        }

        // 5. Spawn a developer for this task
        await SpawnTaskDeveloperAsync(nextTask, ct);
    }

    /// <summary>
    /// Spawns a developer agent for a specific task.
    /// </summary>
    private async Task SpawnTaskDeveloperAsync(TaskItem task, CancellationToken ct)
    {
        var agentRole = $"Developer-{task.TaskId}";
        task.AssignedAgentRole = agentRole;
        await _taskQueue.UpdateTaskStatusAsync(task.TaskId, Enums.TaskStatus.InProgress);

        // Build task context for the developer
        var taskContext = BuildTaskContext(task);

        // Create agent state for this task-developer
        var agentState = new AgentState
        {
            Role = agentRole,
            SubagentType = _taskOptions.DeveloperSubagentType,
            Status = AgentStatus.Queued,
            DependenciesJson = "[]",  // No agent dependencies - task controls ordering
            RecoveryContext = taskContext
        };

        await _stateManager.UpdateAgentStateAsync(agentRole, agentState);

        _logger.LogInformation("Spawning developer {Role} for task {TaskId}: {Title}",
            agentRole, task.TaskId, task.Title);

        // The queued agent will be picked up by ProcessQueuedAgentsAsync
        // Call it immediately to avoid waiting for next cycle
        await ProcessQueuedAgentsAsync(ct);
    }

    /// <summary>
    /// Builds the context string to pass to a task developer.
    /// </summary>
    private string BuildTaskContext(TaskItem task)
    {
        var files = string.IsNullOrEmpty(task.FilesJson)
            ? "No specific files specified"
            : string.Join("\n", System.Text.Json.JsonSerializer.Deserialize<List<string>>(task.FilesJson)!
                .Select(f => $"- {f}"));

        return $"""
            ## Your Assigned Task

            **Task ID:** {task.TaskId}
            **Title:** {task.Title}
            **Phase:** {task.Phase ?? "N/A"}

            ### Description
            {task.Description}

            ### Files to Work On
            {files}

            ### Instructions
            1. Focus ONLY on this specific task
            2. Do NOT add features beyond what is described
            3. Verify the build succeeds before completing
            4. Call `apmas_complete` when done with a summary of changes
            """;
    }
}
