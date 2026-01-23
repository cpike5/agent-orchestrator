using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool for submitting a task breakdown for sequential execution.
/// Called by the Architect agent to define implementation tasks.
/// </summary>
public class SubmitTasksTool : IMcpTool
{
    private readonly ITaskQueueService _taskQueue;
    private readonly ILogger<SubmitTasksTool> _logger;

    public SubmitTasksTool(ITaskQueueService taskQueue, ILogger<SubmitTasksTool> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    public string Name => "apmas_submit_tasks";

    public string Description => "Submit a task breakdown for sequential execution by developer agents";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["agentRole"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The role of the agent submitting tasks (should be 'architect')"
            },
            ["tasks"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Array of tasks to execute in order",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["taskId"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unique task identifier (e.g., 'task-001')"
                        },
                        ["title"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Brief task title"
                        },
                        ["description"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Detailed task instructions"
                        },
                        ["files"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "File paths this task will create or modify",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["phase"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional phase grouping for review checkpoints"
                        }
                    },
                    ["required"] = new JsonArray { "taskId", "title", "description" }
                }
            }
        },
        ["required"] = new JsonArray { "agentRole", "tasks" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        try
        {
            // Parse required agentRole field
            if (!input.TryGetProperty("agentRole", out var agentRoleElement) ||
                agentRoleElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(agentRoleElement.GetString()))
            {
                return ToolResult.Error("Missing or invalid required field: agentRole");
            }
            var agentRole = agentRoleElement.GetString()!;

            // Parse required tasks array
            if (!input.TryGetProperty("tasks", out var tasksElement) ||
                tasksElement.ValueKind != JsonValueKind.Array)
            {
                return ToolResult.Error("Missing or invalid required field: tasks");
            }

            var tasksArray = tasksElement.EnumerateArray().ToList();
            if (tasksArray.Count == 0)
            {
                return ToolResult.Error("Tasks array cannot be empty");
            }

            // Parse and validate each task
            var tasks = new List<TaskItem>();
            var sequence = 1;
            var taskIds = new HashSet<string>();

            foreach (var taskElement in tasksArray)
            {
                // Validate taskId
                if (!taskElement.TryGetProperty("taskId", out var taskIdElement) ||
                    taskIdElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(taskIdElement.GetString()))
                {
                    return ToolResult.Error($"Task {sequence} missing or invalid required field: taskId");
                }
                var taskId = taskIdElement.GetString()!;

                // Check for duplicate taskIds
                if (!taskIds.Add(taskId))
                {
                    return ToolResult.Error($"Duplicate taskId detected: {taskId}");
                }

                // Validate title
                if (!taskElement.TryGetProperty("title", out var titleElement) ||
                    titleElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(titleElement.GetString()))
                {
                    return ToolResult.Error($"Task {taskId} missing or invalid required field: title");
                }
                var title = titleElement.GetString()!;

                // Validate description
                if (!taskElement.TryGetProperty("description", out var descriptionElement) ||
                    descriptionElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(descriptionElement.GetString()))
                {
                    return ToolResult.Error($"Task {taskId} missing or invalid required field: description");
                }
                var description = descriptionElement.GetString()!;

                // Parse optional files array
                string? filesJson = null;
                if (taskElement.TryGetProperty("files", out var filesElement) &&
                    filesElement.ValueKind == JsonValueKind.Array)
                {
                    var files = filesElement.EnumerateArray()
                        .Where(f => f.ValueKind == JsonValueKind.String)
                        .Select(f => f.GetString())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Cast<string>()
                        .ToList();

                    if (files.Count > 0)
                    {
                        filesJson = JsonSerializer.Serialize(files);
                    }
                }

                // Parse optional phase
                string? phase = null;
                if (taskElement.TryGetProperty("phase", out var phaseElement) &&
                    phaseElement.ValueKind == JsonValueKind.String)
                {
                    phase = phaseElement.GetString();
                }

                // Create TaskItem
                var task = new TaskItem
                {
                    TaskId = taskId,
                    Title = title,
                    Description = description,
                    FilesJson = filesJson,
                    Phase = phase,
                    SequenceNumber = sequence++
                };

                tasks.Add(task);
            }

            // Submit tasks to the queue
            await _taskQueue.SubmitTasksAsync(tasks);

            _logger.LogInformation("Agent {AgentRole} submitted {Count} tasks", agentRole, tasks.Count);

            // Build success response
            var response = new JsonObject
            {
                ["message"] = $"Successfully submitted {tasks.Count} tasks",
                ["taskIds"] = new JsonArray(tasks.Select(t => JsonValue.Create(t.TaskId)).ToArray())
            };

            var jsonResponse = response.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return ToolResult.Success(jsonResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse submit_tasks input JSON");
            return ToolResult.Error($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error submitting tasks");
            throw; // Unexpected exceptions should bubble up per IMcpTool contract
        }
    }
}
