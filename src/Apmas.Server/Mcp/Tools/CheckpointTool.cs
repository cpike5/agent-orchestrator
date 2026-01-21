using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to save progress checkpoints.
/// Agents should call this after completing subtasks to enable recovery from timeouts.
/// </summary>
public class CheckpointTool : IMcpTool
{
    private readonly IAgentStateManager _stateManager;
    private readonly IStateStore _stateStore;
    private readonly ILogger<CheckpointTool> _logger;

    public CheckpointTool(
        IAgentStateManager stateManager,
        IStateStore stateStore,
        ILogger<CheckpointTool> logger)
    {
        _stateManager = stateManager;
        _stateStore = stateStore;
        _logger = logger;
    }

    public string Name => "apmas_checkpoint";

    public string Description => "Save a checkpoint of current progress. Call this after completing subtasks to enable recovery from timeouts or context limit issues.";

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
            ["summary"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Summary of current progress"
            },
            ["completedItems"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "List of completed items"
            },
            ["pendingItems"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "List of pending items"
            },
            ["activeFiles"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Files currently being worked on"
            },
            ["notes"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Additional notes for continuation"
            }
        },
        ["required"] = new JsonArray { "agentRole", "summary", "completedItems", "pendingItems" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        try
        {
            // Parse required fields
            if (!input.TryGetProperty("agentRole", out var agentRoleElement) ||
                string.IsNullOrWhiteSpace(agentRoleElement.GetString()))
            {
                return ToolResult.Error("Missing or empty required field: agentRole");
            }
            string agentRole = agentRoleElement.GetString()!;

            if (!input.TryGetProperty("summary", out var summaryElement) ||
                string.IsNullOrWhiteSpace(summaryElement.GetString()))
            {
                return ToolResult.Error("Missing or empty required field: summary");
            }
            string summary = summaryElement.GetString()!;

            if (!input.TryGetProperty("completedItems", out var completedItemsElement))
            {
                return ToolResult.Error("Missing required field: completedItems");
            }
            string[] completedItems = completedItemsElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            if (!input.TryGetProperty("pendingItems", out var pendingItemsElement))
            {
                return ToolResult.Error("Missing required field: pendingItems");
            }
            string[] pendingItems = pendingItemsElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            // Parse optional fields
            string[]? activeFiles = null;
            if (input.TryGetProperty("activeFiles", out var activeFilesElement))
            {
                activeFiles = activeFilesElement.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
            }

            string? notes = null;
            if (input.TryGetProperty("notes", out var notesElement))
            {
                notes = notesElement.GetString();
            }

            // Verify agent exists before saving checkpoint
            try
            {
                var agentState = await _stateManager.GetAgentStateAsync(agentRole);
                if (agentState == null)
                {
                    return ToolResult.Error($"Agent with role '{agentRole}' not found");
                }
            }
            catch (KeyNotFoundException)
            {
                return ToolResult.Error($"Agent with role '{agentRole}' not found");
            }

            // Create checkpoint
            var checkpoint = new Checkpoint
            {
                AgentRole = agentRole,
                Summary = summary,
                CompletedTaskCount = completedItems.Length,
                TotalTaskCount = completedItems.Length + pendingItems.Length,
                CompletedItemsJson = JsonSerializer.Serialize(completedItems),
                PendingItemsJson = JsonSerializer.Serialize(pendingItems),
                ActiveFilesJson = activeFiles != null ? JsonSerializer.Serialize(activeFiles) : null,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            // Save checkpoint
            await _stateStore.SaveCheckpointAsync(checkpoint);

            // Log success
            _logger.LogInformation(
                "Checkpoint saved for {AgentRole}: {PercentComplete:F1}% complete",
                agentRole,
                checkpoint.PercentComplete);

            // Return success response
            return ToolResult.Success(
                $"Checkpoint saved: {checkpoint.CompletedTaskCount}/{checkpoint.TotalTaskCount} items complete ({checkpoint.PercentComplete:F1}%)");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse checkpoint input JSON");
            return ToolResult.Error($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error saving checkpoint");
            throw; // Unexpected exceptions should bubble up per IMcpTool contract
        }
    }
}
