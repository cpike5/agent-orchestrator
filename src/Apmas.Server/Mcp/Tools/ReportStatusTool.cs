using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to report their current status, progress, and artifacts.
/// </summary>
public class ReportStatusTool : IMcpTool
{
    private readonly IAgentStateManager _stateManager;
    private readonly IMessageBus _messageBus;
    private readonly IDashboardEventPublisher _dashboardEvents;
    private readonly ILogger<ReportStatusTool> _logger;

    public ReportStatusTool(
        IAgentStateManager stateManager,
        IMessageBus messageBus,
        IDashboardEventPublisher dashboardEvents,
        ILogger<ReportStatusTool> logger)
    {
        _stateManager = stateManager;
        _messageBus = messageBus;
        _dashboardEvents = dashboardEvents;
        _logger = logger;
    }

    public string Name => "apmas_report_status";

    public string Description => "Report agent status, progress, and artifacts to the orchestrator";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agentRole"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Role of the calling agent"
            },
            ["status"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "working", "done", "blocked", "needs_review", "context_limit" },
                ["description"] = "Current status of the agent"
            },
            ["message"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Status message describing current progress or state"
            },
            ["artifacts"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Array of artifact file paths produced by the agent"
            },
            ["blockedReason"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Reason if blocked (required when status is 'blocked')"
            }
        },
        ["required"] = new JsonArray { "agentRole", "status", "message" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        try
        {
            // Parse input
            if (!input.TryGetProperty("agentRole", out var agentRoleElement))
            {
                return ToolResult.Error("Missing required field: agentRole");
            }
            var agentRole = agentRoleElement.GetString();
            if (string.IsNullOrWhiteSpace(agentRole))
            {
                return ToolResult.Error("agentRole cannot be empty");
            }

            if (!input.TryGetProperty("status", out var statusElement))
            {
                return ToolResult.Error("Missing required field: status");
            }
            var statusString = statusElement.GetString();
            if (string.IsNullOrWhiteSpace(statusString))
            {
                return ToolResult.Error("status cannot be empty");
            }

            if (!input.TryGetProperty("message", out var messageElement))
            {
                return ToolResult.Error("Missing required field: message");
            }
            var message = messageElement.GetString();
            if (string.IsNullOrWhiteSpace(message))
            {
                return ToolResult.Error("message cannot be empty");
            }

            // Optional fields
            string? blockedReason = null;
            if (input.TryGetProperty("blockedReason", out var blockedReasonElement))
            {
                blockedReason = blockedReasonElement.GetString();
            }

            List<string>? artifacts = null;
            if (input.TryGetProperty("artifacts", out var artifactsElement) && artifactsElement.ValueKind == JsonValueKind.Array)
            {
                artifacts = new List<string>();
                foreach (var item in artifactsElement.EnumerateArray())
                {
                    var path = item.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        artifacts.Add(path);
                    }
                }
            }

            // Validate blocked status
            if (statusString == "blocked" && string.IsNullOrWhiteSpace(blockedReason))
            {
                return ToolResult.Error("blockedReason is required when status is 'blocked'");
            }

            // Map status string to AgentStatus and MessageType
            var (agentStatus, messageType, finalMessage, lastError) = MapStatus(statusString, message, blockedReason);

            _logger.LogInformation("Agent {AgentRole} reporting status: {Status} - {Message}", agentRole, statusString, message);

            // Update agent state
            await _stateManager.UpdateAgentStateAsync(agentRole, state =>
            {
                state.Status = agentStatus;
                state.LastMessage = finalMessage;

                if (!string.IsNullOrWhiteSpace(lastError))
                {
                    state.LastError = lastError;
                }

                // Merge artifacts if provided
                if (artifacts != null && artifacts.Count > 0)
                {
                    var existingArtifacts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(state.ArtifactsJson))
                    {
                        try
                        {
                            var existing = JsonSerializer.Deserialize<List<string>>(state.ArtifactsJson);
                            if (existing != null)
                            {
                                existingArtifacts.AddRange(existing);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize existing artifacts for {AgentRole}, starting fresh", agentRole);
                        }
                    }

                    // Add new artifacts, avoiding duplicates
                    foreach (var artifact in artifacts)
                    {
                        if (!existingArtifacts.Contains(artifact))
                        {
                            existingArtifacts.Add(artifact);
                        }
                    }

                    state.ArtifactsJson = JsonSerializer.Serialize(existingArtifacts);
                }

                // Note: CompletedAt should only be set by CompleteTool to ensure consistency
                // ReportStatusTool only updates the status, not completion timestamps

                return state;
            });

            // Publish message to message bus
            var busMessage = new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = agentRole,
                To = "supervisor",
                Type = messageType,
                Content = message,
                ArtifactsJson = artifacts != null && artifacts.Count > 0
                    ? JsonSerializer.Serialize(artifacts)
                    : null,
                Timestamp = DateTime.UtcNow
            };

            await _messageBus.PublishAsync(busMessage);

            _logger.LogDebug("Status update and message published for agent {AgentRole}", agentRole);

            // Publish agent update after status change
            var updatedAgent = await _stateManager.GetAgentStateAsync(agentRole);
            try
            {
                await _dashboardEvents.PublishAgentUpdateAsync(updatedAgent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish dashboard event for agent {AgentRole}", agentRole);
            }

            return ToolResult.Success($"Status updated to '{statusString}'. Message delivered to supervisor.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing status report");
            return ToolResult.Error($"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps the status string to AgentStatus enum, MessageType enum, and derived message/error values.
    /// </summary>
    /// <returns>Tuple of (AgentStatus, MessageType, FinalMessage, LastError)</returns>
    private static (AgentStatus AgentStatus, MessageType MessageType, string FinalMessage, string? LastError) MapStatus(
        string statusString, string message, string? blockedReason)
    {
        return statusString switch
        {
            "working" => (AgentStatus.Running, MessageType.Progress, message, null),
            "done" => (AgentStatus.Completed, MessageType.Done, message, null),
            "blocked" => (AgentStatus.Escalated, MessageType.Blocked, message, blockedReason),
            "needs_review" => (AgentStatus.Running, MessageType.NeedsReview, $"Needs review: {message}", null),
            "context_limit" => (AgentStatus.Paused, MessageType.ContextLimit, message, null),
            _ => throw new ArgumentException($"Unknown status: {statusString}", nameof(statusString))
        };
    }
}
