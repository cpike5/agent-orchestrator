using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to signal alive status and extend their timeout window.
/// Agents should call this every 5 minutes while working to prevent timeout.
/// </summary>
public class HeartbeatTool : IMcpTool
{
    private readonly IAgentStateManager _agentStateManager;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogger<HeartbeatTool> _logger;

    public HeartbeatTool(
        IAgentStateManager agentStateManager,
        IHeartbeatMonitor heartbeatMonitor,
        ILogger<HeartbeatTool> logger)
    {
        _agentStateManager = agentStateManager;
        _heartbeatMonitor = heartbeatMonitor;
        _logger = logger;
    }

    public string Name => "apmas_heartbeat";

    public string Description =>
        "Signal alive status and extend timeout window. Agents should call this every 5 minutes while working.";

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
                ["enum"] = new JsonArray("working", "thinking", "writing"),
                ["description"] = "Current activity status"
            },
            ["progress"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Current progress message"
            },
            ["estimatedContextUsage"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Estimated context usage in tokens"
            }
        },
        ["required"] = new JsonArray("agentRole", "status")
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        // Parse required fields
        if (!input.TryGetProperty("agentRole", out var agentRoleElement) ||
            agentRoleElement.ValueKind != JsonValueKind.String)
        {
            return ToolResult.Error("Missing or invalid required field: agentRole");
        }

        if (!input.TryGetProperty("status", out var statusElement) ||
            statusElement.ValueKind != JsonValueKind.String)
        {
            return ToolResult.Error("Missing or invalid required field: status");
        }

        var agentRole = agentRoleElement.GetString()!;
        var status = statusElement.GetString()!;

        // Validate status enum
        if (status != "working" && status != "thinking" && status != "writing")
        {
            return ToolResult.Error($"Invalid status: {status}. Must be one of: working, thinking, writing");
        }

        // Parse optional fields
        string? progress = null;
        if (input.TryGetProperty("progress", out var progressElement) &&
            progressElement.ValueKind == JsonValueKind.String)
        {
            progress = progressElement.GetString();
        }

        int? estimatedContextUsage = null;
        if (input.TryGetProperty("estimatedContextUsage", out var contextElement) &&
            contextElement.ValueKind == JsonValueKind.Number)
        {
            estimatedContextUsage = contextElement.GetInt32();
        }

        // Update agent state
        try
        {
            await _agentStateManager.UpdateAgentStateAsync(agentRole, state =>
            {
                // Update last message if progress is provided
                if (progress != null)
                {
                    state.LastMessage = progress;
                }

                // Update estimated context usage if provided
                if (estimatedContextUsage.HasValue)
                {
                    state.EstimatedContextUsage = estimatedContextUsage.Value;
                }

                // Update last heartbeat timestamp
                state.LastHeartbeat = DateTime.UtcNow;

                // Extend timeout by 10 minutes from last heartbeat
                state.TimeoutAt = state.LastHeartbeat.Value.AddMinutes(10);

                return state;
            });

            // Record heartbeat in the monitor
            _heartbeatMonitor.RecordHeartbeat(agentRole, status, progress);

            _logger.LogInformation("Heartbeat from {AgentRole}: {Status}", agentRole, status);

            var newTimeoutAt = DateTime.UtcNow.AddMinutes(10);
            var message = $"Heartbeat acknowledged for agent '{agentRole}'. Timeout extended to {newTimeoutAt:yyyy-MM-dd HH:mm:ss} UTC.";

            if (progress != null)
            {
                message += $" Progress: {progress}";
            }

            return ToolResult.Success(message);
        }
        catch (KeyNotFoundException)
        {
            return ToolResult.Error($"Agent with role '{agentRole}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat for agent {AgentRole}", agentRole);
            return ToolResult.Error($"Internal error processing heartbeat: {ex.Message}");
        }
    }
}
