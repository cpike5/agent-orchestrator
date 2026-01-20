using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a message between agents or to the supervisor.
/// </summary>
public class AgentMessage
{
    /// <summary>
    /// Unique identifier for this message.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// When the message was sent.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Role of the sending agent.
    /// </summary>
    public required string From { get; set; }

    /// <summary>
    /// Role of the receiving agent, or "supervisor" or "all".
    /// </summary>
    public required string To { get; set; }

    /// <summary>
    /// Type of message.
    /// </summary>
    public MessageType Type { get; set; }

    /// <summary>
    /// Message content.
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// JSON-serialized list of artifact file paths.
    /// </summary>
    public string? ArtifactsJson { get; set; }

    /// <summary>
    /// JSON-serialized metadata dictionary.
    /// </summary>
    public string? MetadataJson { get; set; }
}
