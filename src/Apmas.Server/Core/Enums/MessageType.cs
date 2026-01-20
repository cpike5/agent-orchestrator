namespace Apmas.Server.Core.Enums;

/// <summary>
/// Types of messages exchanged between agents.
/// </summary>
public enum MessageType
{
    /// <summary>Task assignment to an agent.</summary>
    Assignment,

    /// <summary>Progress update.</summary>
    Progress,

    /// <summary>Question from one agent to another.</summary>
    Question,

    /// <summary>Answer to a question.</summary>
    Answer,

    /// <summary>Heartbeat signal.</summary>
    Heartbeat,

    /// <summary>Checkpoint save notification.</summary>
    Checkpoint,

    /// <summary>Task completion notification.</summary>
    Done,

    /// <summary>Request for review.</summary>
    NeedsReview,

    /// <summary>Review approved.</summary>
    Approved,

    /// <summary>Review requested changes.</summary>
    ChangesRequested,

    /// <summary>Agent is blocked.</summary>
    Blocked,

    /// <summary>Agent approaching context limit.</summary>
    ContextLimit,

    /// <summary>Error occurred.</summary>
    Error
}
