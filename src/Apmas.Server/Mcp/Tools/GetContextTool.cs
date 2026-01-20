using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Services;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to get project context, other agents' outputs, and messages.
/// This enables agents to coordinate work and understand the current project state.
/// </summary>
public class GetContextTool : IMcpTool
{
    private readonly IAgentStateManager _stateManager;
    private readonly IMessageBus _messageBus;
    private readonly IStateStore _stateStore;
    private readonly ILogger<GetContextTool> _logger;

    public GetContextTool(
        IAgentStateManager stateManager,
        IMessageBus messageBus,
        IStateStore stateStore,
        ILogger<GetContextTool> logger)
    {
        _stateManager = stateManager;
        _messageBus = messageBus;
        _stateStore = stateStore;
        _logger = logger;
    }

    public string Name => "apmas_get_context";

    public string Description => "Get current project context, other agents' outputs, and messages. Use this to coordinate work and understand the project state.";

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["include"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray { "project", "agents", "messages", "artifacts" }
                },
                ["description"] = "What to include in the context response (default: all)"
            },
            ["agentRoles"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Filter to specific agent roles"
            },
            ["messageLimit"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "Maximum number of messages to return (default: 50)"
            }
        }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        try
        {
            // Parse include parameter (default to all)
            var include = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (input.TryGetProperty("include", out var includeElement))
            {
                foreach (var item in includeElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        include.Add(value);
                    }
                }
            }
            else
            {
                // Default to all
                include = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "project", "agents", "messages", "artifacts" };
            }

            // Parse agentRoles filter
            List<string>? agentRoles = null;
            if (input.TryGetProperty("agentRoles", out var agentRolesElement))
            {
                agentRoles = agentRolesElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Cast<string>()
                    .ToList();
            }

            // Parse messageLimit (default 50)
            int messageLimit = 50;
            if (input.TryGetProperty("messageLimit", out var messageLimitElement))
            {
                messageLimit = messageLimitElement.GetInt32();
                if (messageLimit < 1)
                {
                    messageLimit = 50;
                }
            }

            // Build response object
            var response = new JsonObject();

            // Add project state if requested
            if (include.Contains("project"))
            {
                var projectState = await _stateManager.GetProjectStateAsync();
                response["project"] = new JsonObject
                {
                    ["name"] = projectState.Name,
                    ["workingDirectory"] = projectState.WorkingDirectory,
                    ["phase"] = projectState.Phase.ToString(),
                    ["startedAt"] = projectState.StartedAt.ToString("O")
                };

                _logger.LogInformation("Retrieved project state for context");
            }

            // Add agent states if requested
            if (include.Contains("agents"))
            {
                IReadOnlyList<Core.Models.AgentState> agents;

                if (agentRoles != null && agentRoles.Count > 0)
                {
                    // Get specific agents by role
                    var agentTasks = agentRoles.Select(role => _stateManager.GetAgentStateAsync(role));
                    agents = (await Task.WhenAll(agentTasks)).ToList();
                }
                else
                {
                    // Get all agents (not just active ones)
                    agents = await _stateStore.GetAllAgentStatesAsync();
                }

                var agentsArray = new JsonArray();
                foreach (var agent in agents)
                {
                    agentsArray.Add(new JsonObject
                    {
                        ["role"] = agent.Role,
                        ["status"] = agent.Status.ToString(),
                        ["lastMessage"] = agent.LastMessage,
                        ["spawnedAt"] = agent.SpawnedAt?.ToString("O")
                    });
                }
                response["agents"] = agentsArray;

                _logger.LogInformation("Retrieved {AgentCount} agent states for context", agents.Count);
            }

            // Add messages if requested
            if (include.Contains("messages"))
            {
                var messages = await _messageBus.GetAllMessagesAsync(messageLimit);

                var messagesArray = new JsonArray();
                foreach (var message in messages)
                {
                    messagesArray.Add(new JsonObject
                    {
                        ["from"] = message.From,
                        ["to"] = message.To,
                        ["type"] = message.Type.ToString(),
                        ["content"] = message.Content,
                        ["timestamp"] = message.Timestamp.ToString("O")
                    });
                }
                response["messages"] = messagesArray;

                _logger.LogInformation("Retrieved {MessageCount} messages for context", messages.Count);
            }

            // Add artifacts if requested
            if (include.Contains("artifacts"))
            {
                IReadOnlyList<Core.Models.AgentState> agents;

                if (agentRoles != null && agentRoles.Count > 0)
                {
                    // Get specific agents by role
                    var agentTasks = agentRoles.Select(role => _stateManager.GetAgentStateAsync(role));
                    agents = (await Task.WhenAll(agentTasks)).ToList();
                }
                else
                {
                    // Get all agents
                    agents = await _stateStore.GetAllAgentStatesAsync();
                }

                // Collect and flatten artifacts
                var artifactsList = new List<string>();
                foreach (var agent in agents)
                {
                    if (!string.IsNullOrWhiteSpace(agent.ArtifactsJson))
                    {
                        try
                        {
                            var artifacts = JsonSerializer.Deserialize<string[]>(agent.ArtifactsJson);
                            if (artifacts != null)
                            {
                                artifactsList.AddRange(artifacts);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize artifacts for agent {AgentRole}", agent.Role);
                        }
                    }
                }

                var artifactsArray = new JsonArray();
                foreach (var artifact in artifactsList.Distinct())
                {
                    artifactsArray.Add(artifact);
                }
                response["artifacts"] = artifactsArray;

                _logger.LogInformation("Retrieved {ArtifactCount} artifacts for context", artifactsList.Count);
            }

            // Serialize and return
            var jsonResponse = response.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return ToolResult.Success(jsonResponse);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse get_context input JSON");
            return ToolResult.Error($"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving context");
            throw; // Unexpected exceptions should bubble up per IMcpTool contract
        }
    }
}
