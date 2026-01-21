namespace Apmas.Server.Core.Models;

/// <summary>
/// Status of an agent process.
/// </summary>
public enum AgentProcessStatus
{
    /// <summary>
    /// Process is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Process has exited normally.
    /// </summary>
    Exited,

    /// <summary>
    /// Process was forcefully terminated.
    /// </summary>
    Terminated,

    /// <summary>
    /// Process status is unknown.
    /// </summary>
    Unknown
}

/// <summary>
/// Information about a running or terminated agent process.
/// </summary>
public record AgentProcessInfo
{
    /// <summary>
    /// Process ID of the agent.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Agent role identifier.
    /// </summary>
    public required string AgentRole { get; init; }

    /// <summary>
    /// When the agent process was started.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Current status of the process.
    /// </summary>
    public AgentProcessStatus Status { get; init; }

    /// <summary>
    /// Exit code if the process has exited.
    /// </summary>
    public int? ExitCode { get; init; }
}
