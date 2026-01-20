using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to request help from humans, other agents, or request clarification.
/// </summary>
public class RequestHelpTool : IMcpTool
{
    private readonly IAgentStateManager _stateManager;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<RequestHelpTool> _logger;

    public RequestHelpTool(
        IAgentStateManager stateManager,
        IMessageBus messageBus,
        ILogger<RequestHelpTool> logger)
    {
        _stateManager = stateManager;
        _messageBus = messageBus;
        _logger = logger;
    }

    public string Name => "apmas_request_help";

    public string Description => "Request help from human, another agent, or request clarification from supervisor";

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
            ["helpType"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "human", "agent", "clarification" },
                ["description"] = "Type of help being requested"
            },
            ["targetAgent"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Target agent role (required when helpType is 'agent')"
            },
            ["issue"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Description of the issue or question"
            },
            ["context"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Additional context to help understand the issue"
            }
        },
        ["required"] = new JsonArray { "agentRole", "helpType", "issue" }
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

            if (!input.TryGetProperty("helpType", out var helpTypeElement))
            {
                return ToolResult.Error("Missing required field: helpType");
            }
            var helpType = helpTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(helpType))
            {
                return ToolResult.Error("helpType cannot be empty");
            }

            if (!input.TryGetProperty("issue", out var issueElement))
            {
                return ToolResult.Error("Missing required field: issue");
            }
            var issue = issueElement.GetString();
            if (string.IsNullOrWhiteSpace(issue))
            {
                return ToolResult.Error("issue cannot be empty");
            }

            // Parse optional fields
            string? targetAgent = null;
            if (input.TryGetProperty("targetAgent", out var targetAgentElement))
            {
                targetAgent = targetAgentElement.GetString();
            }

            string? context = null;
            if (input.TryGetProperty("context", out var contextElement))
            {
                context = contextElement.GetString();
            }

            // Validate helpType-specific requirements
            if (helpType == "agent" && string.IsNullOrWhiteSpace(targetAgent))
            {
                return ToolResult.Error("targetAgent is required when helpType is 'agent'");
            }

            // Build full message with context
            var fullMessage = string.IsNullOrWhiteSpace(context)
                ? issue
                : $"{issue}\n\nContext: {context}";

            _logger.LogInformation(
                "Agent {AgentRole} requesting {HelpType} help: {Issue}",
                agentRole,
                helpType,
                issue);

            // Handle help request based on type
            string responseMessage;
            switch (helpType)
            {
                case "human":
                    responseMessage = await HandleHumanHelpAsync(agentRole, issue, fullMessage);
                    break;

                case "agent":
                    responseMessage = await HandleAgentHelpAsync(agentRole, targetAgent!, issue, fullMessage);
                    break;

                case "clarification":
                    responseMessage = await HandleClarificationAsync(agentRole, issue, fullMessage);
                    break;

                default:
                    return ToolResult.Error($"Invalid helpType: {helpType}. Must be 'human', 'agent', or 'clarification'");
            }

            return ToolResult.Success(responseMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing help request");
            return ToolResult.Error($"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles a request for human help by escalating the agent.
    /// </summary>
    private async Task<string> HandleHumanHelpAsync(string agentRole, string issue, string fullMessage)
    {
        // Update agent state to Escalated
        await _stateManager.UpdateAgentStateAsync(agentRole, state =>
        {
            state.Status = AgentStatus.Escalated;
            state.LastMessage = $"Requesting human help: {issue}";
            state.LastError = fullMessage;
            return state;
        });

        // Send message to supervisor for notification
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = agentRole,
            To = "supervisor",
            Type = MessageType.Request,
            Content = $"HUMAN HELP REQUESTED: {fullMessage}"
        };

        await _messageBus.PublishAsync(message);

        _logger.LogWarning(
            "Agent {AgentRole} escalated to human. Issue: {Issue}",
            agentRole,
            issue);

        return "Your request has been escalated to human review. Your status has been set to 'Escalated' and work will pause until assistance is provided.";
    }

    /// <summary>
    /// Handles a request for help from another agent.
    /// </summary>
    private async Task<string> HandleAgentHelpAsync(string agentRole, string targetAgent, string issue, string fullMessage)
    {
        // Send question message to target agent
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = agentRole,
            To = targetAgent,
            Type = MessageType.Question,
            Content = fullMessage
        };

        await _messageBus.PublishAsync(message);

        _logger.LogInformation(
            "Agent {AgentRole} sent question to {TargetAgent}: {Issue}",
            agentRole,
            targetAgent,
            issue);

        return $"Your question has been sent to agent '{targetAgent}'. You can continue working or wait for their response using apmas_get_context to check for messages.";
    }

    /// <summary>
    /// Handles a clarification request to the supervisor.
    /// </summary>
    private async Task<string> HandleClarificationAsync(string agentRole, string issue, string fullMessage)
    {
        // Send question message to supervisor
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = agentRole,
            To = "supervisor",
            Type = MessageType.Question,
            Content = $"CLARIFICATION NEEDED: {fullMessage}"
        };

        await _messageBus.PublishAsync(message);

        _logger.LogInformation(
            "Agent {AgentRole} requesting clarification from supervisor: {Issue}",
            agentRole,
            issue);

        return "Your clarification request has been sent to the supervisor. Continue working on tasks that don't require this information, or use apmas_get_context to check for a response.";
    }
}
