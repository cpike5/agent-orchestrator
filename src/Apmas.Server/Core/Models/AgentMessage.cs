using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a message exchanged between agents and the supervisor.
/// </summary>
public record AgentMessage
{
    /// <summary>
    /// Unique identifier for the message.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Role of the agent that sent the message.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// Role of the target agent, or "supervisor" or "all".
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// Type of message.
    /// </summary>
    public required MessageType Type { get; init; }

    /// <summary>
    /// Message content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Optional list of artifact file paths related to this message.
    /// </summary>
    public IReadOnlyList<string>? Artifacts { get; init; }

    /// <summary>
    /// Optional metadata dictionary for additional context.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}
