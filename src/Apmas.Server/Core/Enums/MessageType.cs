namespace Apmas.Server.Core.Enums;

/// <summary>
/// Types of messages exchanged between agents and the supervisor.
/// </summary>
public enum MessageType
{
    /// <summary>Task assignment from supervisor to agent.</summary>
    Assignment,

    /// <summary>Progress update from agent.</summary>
    Progress,

    /// <summary>Question from one agent to another or to human.</summary>
    Question,

    /// <summary>Answer to a question.</summary>
    Answer,

    /// <summary>Periodic heartbeat signal.</summary>
    Heartbeat,

    /// <summary>Checkpoint save notification.</summary>
    Checkpoint,

    /// <summary>Task completion notification.</summary>
    Done,

    /// <summary>Agent requesting review of their work.</summary>
    NeedsReview,

    /// <summary>Work approved by reviewer.</summary>
    Approved,

    /// <summary>Reviewer requested changes.</summary>
    ChangesRequested,

    /// <summary>Agent is blocked and needs help.</summary>
    Blocked,

    /// <summary>Agent approaching or hit context limits.</summary>
    ContextLimit,

    /// <summary>Error notification.</summary>
    Error
}
