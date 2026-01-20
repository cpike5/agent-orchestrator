using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents the state of an individual agent.
/// </summary>
public class AgentState
{
    /// <summary>
    /// Primary key for EF Core.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Role identifier for this agent (e.g., "architect", "developer").
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Current status of the agent.
    /// </summary>
    public AgentStatus Status { get; set; } = AgentStatus.Pending;

    /// <summary>
    /// Subagent type to use when spawning (e.g., "systems-architect").
    /// </summary>
    public required string SubagentType { get; set; }

    /// <summary>
    /// When the agent was spawned.
    /// </summary>
    public DateTime? SpawnedAt { get; set; }

    /// <summary>
    /// When the agent completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When the agent will timeout.
    /// </summary>
    public DateTime? TimeoutAt { get; set; }

    /// <summary>
    /// Task ID from the agent spawner.
    /// </summary>
    public string? TaskId { get; set; }

    /// <summary>
    /// Number of times this agent has been retried.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// JSON-serialized list of artifact file paths.
    /// </summary>
    public string? ArtifactsJson { get; set; }

    /// <summary>
    /// JSON-serialized list of dependency role names.
    /// </summary>
    public string? DependenciesJson { get; set; }

    /// <summary>
    /// Last status message from the agent.
    /// </summary>
    public string? LastMessage { get; set; }

    /// <summary>
    /// Last error message from the agent.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Estimated context usage in tokens.
    /// </summary>
    public int? EstimatedContextUsage { get; set; }
}
