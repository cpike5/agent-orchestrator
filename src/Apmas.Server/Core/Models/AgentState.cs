using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents the state of an individual agent.
/// </summary>
public record AgentState
{
    /// <summary>
    /// Unique identifier for the agent state record.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Unique role name for the agent (e.g., "architect", "developer").
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    public required AgentStatus Status { get; init; }

    /// <summary>
    /// The Claude Code subagent type to use.
    /// </summary>
    public required string SubagentType { get; init; }

    /// <summary>
    /// When the agent was spawned.
    /// </summary>
    public DateTime? SpawnedAt { get; init; }

    /// <summary>
    /// When the agent completed its task.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// When the agent will timeout if no heartbeat received.
    /// </summary>
    public DateTime? TimeoutAt { get; init; }

    /// <summary>
    /// Optional task/process ID for the running agent.
    /// </summary>
    public string? TaskId { get; init; }

    /// <summary>
    /// Number of times the agent has been restarted.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// List of artifact file paths created by the agent.
    /// </summary>
    public IReadOnlyList<string> Artifacts { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of agent roles this agent depends on.
    /// </summary>
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Last status message from the agent.
    /// </summary>
    public string? LastMessage { get; init; }

    /// <summary>
    /// Last error message from the agent.
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Estimated context token usage (if known).
    /// </summary>
    public int? EstimatedContextUsage { get; init; }
}
