using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to signal task completion with summary and artifacts.
/// </summary>
public class CompleteTool : IMcpTool
{
    private readonly IAgentStateManager _stateManager;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CompleteTool> _logger;

    public CompleteTool(
        IAgentStateManager stateManager,
        IMessageBus messageBus,
        ILogger<CompleteTool> logger)
    {
        _stateManager = stateManager;
        _messageBus = messageBus;
        _logger = logger;
    }

    public string Name => "apmas_complete";

    public string Description => "Signal task completion with summary and artifacts";

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
                ["description"] = "Summary of work completed"
            },
            ["artifacts"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Array of artifact file paths produced by the agent"
            },
            ["notes"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Optional notes for downstream agents"
            }
        },
        ["required"] = new JsonArray { "agentRole", "summary", "artifacts" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        try
        {
            // Parse required fields
            if (!input.TryGetProperty("agentRole", out var agentRoleElement))
            {
                return ToolResult.Error("Missing required field: agentRole");
            }
            var agentRole = agentRoleElement.GetString();
            if (string.IsNullOrWhiteSpace(agentRole))
            {
                return ToolResult.Error("agentRole cannot be empty");
            }

            if (!input.TryGetProperty("summary", out var summaryElement))
            {
                return ToolResult.Error("Missing required field: summary");
            }
            var summary = summaryElement.GetString();
            if (string.IsNullOrWhiteSpace(summary))
            {
                return ToolResult.Error("summary cannot be empty");
            }

            if (!input.TryGetProperty("artifacts", out var artifactsElement))
            {
                return ToolResult.Error("Missing required field: artifacts");
            }
            if (artifactsElement.ValueKind != JsonValueKind.Array)
            {
                return ToolResult.Error("artifacts must be an array");
            }

            var artifacts = new List<string>();
            foreach (var item in artifactsElement.EnumerateArray())
            {
                var path = item.GetString();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    artifacts.Add(path);
                }
            }

            // Optional notes field
            string? notes = null;
            if (input.TryGetProperty("notes", out var notesElement))
            {
                notes = notesElement.GetString();
            }

            _logger.LogInformation("Agent {AgentRole} completing task with {ArtifactCount} artifacts",
                agentRole, artifacts.Count);

            // Calculate duration if SpawnedAt is available
            TimeSpan? duration = null;
            var state = await _stateManager.GetAgentStateAsync(agentRole);
            if (state?.SpawnedAt != null)
            {
                duration = DateTime.UtcNow - state.SpawnedAt.Value;
            }

            // Update agent state
            await _stateManager.UpdateAgentStateAsync(agentRole, agentState =>
            {
                agentState.Status = AgentStatus.Completed;
                agentState.CompletedAt = DateTime.UtcNow;
                agentState.ArtifactsJson = JsonSerializer.Serialize(artifacts);
                agentState.LastMessage = summary;
                return agentState;
            });

            // Build message content including notes if provided
            var messageContent = summary;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                messageContent = $"{summary}\n\nNotes for downstream agents:\n{notes}";
            }

            // Publish Done message to MessageBus
            var message = new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = agentRole,
                To = "supervisor",
                Type = MessageType.Done,
                Content = messageContent,
                ArtifactsJson = JsonSerializer.Serialize(artifacts)
            };

            await _messageBus.PublishAsync(message);

            // Log completion with duration if available
            if (duration.HasValue)
            {
                _logger.LogInformation("Agent {AgentRole} completed in {Duration:hh\\:mm\\:ss}",
                    agentRole, duration.Value);
            }
            else
            {
                _logger.LogInformation("Agent {AgentRole} completed successfully", agentRole);
            }

            return ToolResult.Success(
                $"Task completed successfully. You can now stop. Duration: {duration?.ToString(@"hh\:mm\:ss") ?? "unknown"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task completion");
            return ToolResult.Error($"Internal error: {ex.Message}");
        }
    }
}
