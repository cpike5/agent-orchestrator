using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Tools;

/// <summary>
/// MCP tool that allows agents to send messages to other agents or broadcast to all.
/// </summary>
public class SendMessageTool : IMcpTool
{
    private readonly IMessageBus _messageBus;
    private readonly ILogger<SendMessageTool> _logger;

    public SendMessageTool(
        IMessageBus messageBus,
        ILogger<SendMessageTool> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    public string Name => "apmas_send_message";

    public string Description => "Send a message to another agent or broadcast to all agents";

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
            ["to"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Target agent role or 'all' for broadcast"
            },
            ["type"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "question", "answer", "info", "request" },
                ["description"] = "Type of message"
            },
            ["content"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Message content"
            }
        },
        ["required"] = new JsonArray { "agentRole", "to", "type", "content" }
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

            if (!input.TryGetProperty("to", out var toElement))
            {
                return ToolResult.Error("Missing required field: to");
            }
            var to = toElement.GetString();
            if (string.IsNullOrWhiteSpace(to))
            {
                return ToolResult.Error("to cannot be empty");
            }

            if (!input.TryGetProperty("type", out var typeElement))
            {
                return ToolResult.Error("Missing required field: type");
            }
            var typeString = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(typeString))
            {
                return ToolResult.Error("type cannot be empty");
            }

            if (!input.TryGetProperty("content", out var contentElement))
            {
                return ToolResult.Error("Missing required field: content");
            }
            var content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return ToolResult.Error("content cannot be empty");
            }

            // Map type string to MessageType enum
            var messageType = MapMessageType(typeString);
            if (!messageType.HasValue)
            {
                return ToolResult.Error($"Invalid message type: {typeString}. Must be one of: question, answer, info, request");
            }

            // Create and publish message
            var message = new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = agentRole,
                To = to,
                Type = messageType.Value,
                Content = content
            };

            await _messageBus.PublishAsync(message);

            var destination = to.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "all agents"
                : $"agent '{to}'";

            _logger.LogInformation(
                "Agent {AgentRole} sent {MessageType} message to {Destination}: {MessageId}",
                agentRole,
                typeString,
                destination,
                message.Id);

            return ToolResult.Success($"Message sent to {destination}. Message ID: {message.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return ToolResult.Error($"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps the message type string to MessageType enum.
    /// </summary>
    private static MessageType? MapMessageType(string typeString)
    {
        return typeString.ToLowerInvariant() switch
        {
            "question" => MessageType.Question,
            "answer" => MessageType.Answer,
            "info" => MessageType.Info,
            "request" => MessageType.Request,
            _ => null
        };
    }
}
