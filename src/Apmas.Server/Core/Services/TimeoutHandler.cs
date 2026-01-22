using System.Text;
using System.Text.Json;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Handles agent timeouts with progressive retry strategies.
/// </summary>
public class TimeoutHandler : ITimeoutHandler
{
    private readonly IAgentStateManager _stateManager;
    private readonly IStateStore _stateStore;
    private readonly IMessageBus _messageBus;
    private readonly IDashboardEventPublisher _dashboardEvents;
    private readonly ILogger<TimeoutHandler> _logger;
    private readonly ApmasOptions _options;

    public TimeoutHandler(
        IAgentStateManager stateManager,
        IStateStore stateStore,
        IMessageBus messageBus,
        IDashboardEventPublisher dashboardEvents,
        ILogger<TimeoutHandler> logger,
        IOptions<ApmasOptions> options)
    {
        _stateManager = stateManager;
        _stateStore = stateStore;
        _messageBus = messageBus;
        _dashboardEvents = dashboardEvents;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task HandleTimeoutAsync(string agentRole, CancellationToken cancellationToken = default)
    {
        var agent = await _stateManager.GetAgentStateAsync(agentRole);
        var maxRetries = _options.Timeouts.MaxRetries;

        _logger.LogWarning(
            "Handling timeout for agent {AgentRole} (retry count: {RetryCount}/{MaxRetries})",
            agentRole,
            agent.RetryCount,
            maxRetries);

        // Determine strategy based on retry count
        // RetryCount 0 = first timeout (hasn't retried yet)
        // RetryCount 1 = second timeout (retried once)
        // RetryCount 2+ = third+ timeout (retried twice or more)
        if (agent.RetryCount >= maxRetries - 1)
        {
            // Third or more timeout - escalate
            await EscalateToHumanAsync(agent, cancellationToken);
        }
        else if (agent.RetryCount == 1)
        {
            // Second timeout - restart with reduced scope
            await RestartWithReducedScopeAsync(agent, cancellationToken);
        }
        else
        {
            // First timeout - restart with checkpoint
            await RestartWithCheckpointAsync(agent, cancellationToken);
        }
    }

    /// <summary>
    /// Restarts the agent with checkpoint context (first timeout strategy).
    /// </summary>
    private async Task RestartWithCheckpointAsync(AgentState agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "First timeout for agent {AgentRole} - restarting with checkpoint context",
            agent.Role);

        var checkpoint = await _stateStore.GetLatestCheckpointAsync(agent.Role);
        var recoveryContext = checkpoint != null
            ? BuildRecoveryContext(checkpoint, reducedScope: false)
            : null;

        if (checkpoint != null)
        {
            _logger.LogInformation(
                "Found checkpoint for agent {AgentRole}: {Summary} ({PercentComplete:F0}% complete)",
                agent.Role,
                checkpoint.Summary,
                checkpoint.PercentComplete);
        }
        else
        {
            _logger.LogWarning(
                "No checkpoint found for agent {AgentRole} - restarting without recovery context",
                agent.Role);
        }

        // Update agent state to queue for restart
        await _stateManager.UpdateAgentStateAsync(agent.Role, a =>
        {
            a.Status = AgentStatus.Queued;
            a.RetryCount++;
            a.LastError = "Heartbeat timeout - restarting with checkpoint";
            a.TimeoutAt = null;
            a.RecoveryContext = recoveryContext;
            return a;
        });

        _logger.LogInformation(
            "Agent {AgentRole} queued for restart (attempt {RetryCount})",
            agent.Role,
            agent.RetryCount + 1);

        // Publish agent update after restart with checkpoint
        var updatedAgent = await _stateManager.GetAgentStateAsync(agent.Role);
        try
        {
            await _dashboardEvents.PublishAgentUpdateAsync(updatedAgent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agent.Role);
        }
    }

    /// <summary>
    /// Restarts the agent with reduced scope instructions (second timeout strategy).
    /// </summary>
    private async Task RestartWithReducedScopeAsync(AgentState agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Second timeout for agent {AgentRole} - restarting with reduced scope",
            agent.Role);

        var checkpoint = await _stateStore.GetLatestCheckpointAsync(agent.Role);
        var recoveryContext = checkpoint != null
            ? BuildRecoveryContext(checkpoint, reducedScope: true)
            : BuildReducedScopeInstructions();

        if (checkpoint != null)
        {
            _logger.LogInformation(
                "Found checkpoint for agent {AgentRole}: {Summary} ({PercentComplete:F0}% complete) - applying reduced scope",
                agent.Role,
                checkpoint.Summary,
                checkpoint.PercentComplete);
        }

        // Update agent state to queue for restart with reduced scope
        await _stateManager.UpdateAgentStateAsync(agent.Role, a =>
        {
            a.Status = AgentStatus.Queued;
            a.RetryCount++;
            a.LastError = "Heartbeat timeout - restarting with reduced scope";
            a.TimeoutAt = null;
            a.RecoveryContext = recoveryContext;
            return a;
        });

        _logger.LogInformation(
            "Agent {AgentRole} queued for restart with reduced scope (attempt {RetryCount})",
            agent.Role,
            agent.RetryCount + 1);

        // Publish agent update after restart with reduced scope
        var updatedAgent = await _stateManager.GetAgentStateAsync(agent.Role);
        try
        {
            await _dashboardEvents.PublishAgentUpdateAsync(updatedAgent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agent.Role);
        }
    }

    /// <summary>
    /// Escalates the agent to human intervention (third+ timeout strategy).
    /// </summary>
    private async Task EscalateToHumanAsync(AgentState agent, CancellationToken cancellationToken)
    {
        _logger.LogError(
            "Agent {AgentRole} has exceeded max retries ({RetryCount}) - escalating to human",
            agent.Role,
            agent.RetryCount);

        // Get checkpoint for context in escalation message
        var checkpoint = await _stateStore.GetLatestCheckpointAsync(agent.Role);

        // Build escalation message with full context
        var escalationContent = BuildEscalationMessage(agent, checkpoint);

        // Update agent state to escalated
        await _stateManager.UpdateAgentStateAsync(agent.Role, a =>
        {
            a.Status = AgentStatus.Escalated;
            a.LastError = $"Timed out after {agent.RetryCount + 1} attempts - escalated to human";
            a.TimeoutAt = null;
            return a;
        });

        // Send notification to supervisor via message bus
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = agent.Role,
            To = "supervisor",
            Type = MessageType.Error,
            Content = escalationContent,
            Timestamp = DateTime.UtcNow
        };

        await _messageBus.PublishAsync(message);

        _logger.LogWarning(
            "Escalation notification sent for agent {AgentRole}",
            agent.Role);

        // Publish agent update after escalation
        var escalatedAgent = await _stateManager.GetAgentStateAsync(agent.Role);
        try
        {
            await _dashboardEvents.PublishAgentUpdateAsync(escalatedAgent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agent.Role);
        }
    }

    /// <summary>
    /// Builds recovery context from a checkpoint.
    /// </summary>
    private string BuildRecoveryContext(Checkpoint checkpoint, bool reducedScope)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Recovery Context");
        sb.AppendLine();
        sb.AppendLine($"This is a recovery after timeout. Your previous session made progress that you should continue from.");
        sb.AppendLine();

        if (reducedScope)
        {
            sb.AppendLine("### IMPORTANT: Reduced Scope Mode");
            sb.AppendLine();
            sb.AppendLine("This is your second restart. To avoid further timeouts:");
            sb.AppendLine("- Focus ONLY on the remaining pending items listed below");
            sb.AppendLine("- Break each item into the smallest possible atomic tasks");
            sb.AppendLine("- Complete and checkpoint after EACH small task");
            sb.AppendLine("- Do NOT attempt to do multiple things at once");
            sb.AppendLine("- If a task seems complex, break it down further before starting");
            sb.AppendLine();
        }

        sb.AppendLine("### Progress Summary");
        sb.AppendLine();
        sb.AppendLine($"**Status:** {checkpoint.Summary}");
        sb.AppendLine($"**Progress:** {checkpoint.CompletedTaskCount}/{checkpoint.TotalTaskCount} tasks ({checkpoint.PercentComplete:F0}% complete)");
        sb.AppendLine($"**Checkpoint Time:** {checkpoint.CreatedAt:u}");
        sb.AppendLine();

        // Completed items
        if (!string.IsNullOrEmpty(checkpoint.CompletedItemsJson))
        {
            sb.AppendLine("### Completed Items");
            sb.AppendLine();
            try
            {
                var completedItems = JsonSerializer.Deserialize<List<string>>(checkpoint.CompletedItemsJson);
                if (completedItems != null && completedItems.Count > 0)
                {
                    foreach (var item in completedItems)
                    {
                        sb.AppendLine($"- [x] {item}");
                    }
                }
            }
            catch
            {
                sb.AppendLine(checkpoint.CompletedItemsJson);
            }
            sb.AppendLine();
        }

        // Pending items
        if (!string.IsNullOrEmpty(checkpoint.PendingItemsJson))
        {
            sb.AppendLine("### Pending Items (Continue from here)");
            sb.AppendLine();
            try
            {
                var pendingItems = JsonSerializer.Deserialize<List<string>>(checkpoint.PendingItemsJson);
                if (pendingItems != null && pendingItems.Count > 0)
                {
                    foreach (var item in pendingItems)
                    {
                        sb.AppendLine($"- [ ] {item}");
                    }
                }
            }
            catch
            {
                sb.AppendLine(checkpoint.PendingItemsJson);
            }
            sb.AppendLine();
        }

        // Active files
        if (!string.IsNullOrEmpty(checkpoint.ActiveFilesJson))
        {
            sb.AppendLine("### Active Files");
            sb.AppendLine();
            sb.AppendLine("These files were being worked on:");
            sb.AppendLine();
            try
            {
                var activeFiles = JsonSerializer.Deserialize<List<string>>(checkpoint.ActiveFilesJson);
                if (activeFiles != null && activeFiles.Count > 0)
                {
                    foreach (var file in activeFiles)
                    {
                        sb.AppendLine($"- `{file}`");
                    }
                }
            }
            catch
            {
                sb.AppendLine(checkpoint.ActiveFilesJson);
            }
            sb.AppendLine();
        }

        // Notes
        if (!string.IsNullOrEmpty(checkpoint.Notes))
        {
            sb.AppendLine("### Notes");
            sb.AppendLine();
            sb.AppendLine(checkpoint.Notes);
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Please continue from where the previous session left off. Remember to send heartbeats regularly and checkpoint after completing subtasks.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds reduced scope instructions when no checkpoint is available.
    /// </summary>
    private string BuildReducedScopeInstructions()
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Recovery Context - Reduced Scope Mode");
        sb.AppendLine();
        sb.AppendLine("This is a recovery after timeout. No checkpoint was found from your previous session.");
        sb.AppendLine();
        sb.AppendLine("### IMPORTANT: Work in Small Increments");
        sb.AppendLine();
        sb.AppendLine("To avoid further timeouts:");
        sb.AppendLine("- Break your work into the smallest possible atomic tasks");
        sb.AppendLine("- Complete and checkpoint after EACH small task");
        sb.AppendLine("- Send heartbeats regularly (every few minutes)");
        sb.AppendLine("- Do NOT attempt to do multiple things at once");
        sb.AppendLine("- If a task seems complex, break it down further before starting");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Builds a detailed escalation message for human intervention.
    /// </summary>
    private string BuildEscalationMessage(AgentState agent, Checkpoint? checkpoint)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ESCALATION: Agent '{agent.Role}' requires human intervention");
        sb.AppendLine();
        sb.AppendLine("## Reason");
        sb.AppendLine($"Agent timed out {agent.RetryCount + 1} times and has exceeded the maximum retry limit.");
        sb.AppendLine();
        sb.AppendLine("## Agent Details");
        sb.AppendLine($"- **Role:** {agent.Role}");
        sb.AppendLine($"- **Subagent Type:** {agent.SubagentType}");
        sb.AppendLine($"- **Retry Count:** {agent.RetryCount + 1}");
        sb.AppendLine($"- **Last Error:** {agent.LastError ?? "N/A"}");
        sb.AppendLine($"- **Spawned At:** {agent.SpawnedAt?.ToString("u") ?? "N/A"}");
        sb.AppendLine();

        if (checkpoint != null)
        {
            sb.AppendLine("## Last Known Progress");
            sb.AppendLine($"- **Summary:** {checkpoint.Summary}");
            sb.AppendLine($"- **Progress:** {checkpoint.CompletedTaskCount}/{checkpoint.TotalTaskCount} ({checkpoint.PercentComplete:F0}%)");
            sb.AppendLine($"- **Last Checkpoint:** {checkpoint.CreatedAt:u}");

            if (!string.IsNullOrEmpty(checkpoint.Notes))
            {
                sb.AppendLine($"- **Notes:** {checkpoint.Notes}");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Last Known Progress");
            sb.AppendLine("No checkpoint data available.");
            sb.AppendLine();
        }

        sb.AppendLine("## Recommended Actions");
        sb.AppendLine("1. Review the agent's task and determine if it needs to be broken into smaller pieces");
        sb.AppendLine("2. Check for any blocking issues or missing dependencies");
        sb.AppendLine("3. Consider manually completing the remaining work or reassigning to a different agent");

        return sb.ToString();
    }
}
