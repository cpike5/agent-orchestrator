namespace Apmas.Server.Core.Enums;

/// <summary>
/// Represents the lifecycle state of an agent.
/// </summary>
public enum AgentStatus
{
    /// <summary>Waiting for dependencies to complete.</summary>
    Pending,

    /// <summary>Dependencies met, ready to spawn.</summary>
    Queued,

    /// <summary>Being launched.</summary>
    Spawning,

    /// <summary>Actively working.</summary>
    Running,

    /// <summary>Checkpointed, waiting to resume.</summary>
    Paused,

    /// <summary>Successfully finished.</summary>
    Completed,

    /// <summary>Failed (will retry).</summary>
    Failed,

    /// <summary>Exceeded time limit.</summary>
    TimedOut,

    /// <summary>Requires human intervention.</summary>
    Escalated
}
