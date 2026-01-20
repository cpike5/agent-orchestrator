namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents a work item or task that can be assigned to an agent.
/// </summary>
public record WorkItem
{
    /// <summary>
    /// Unique identifier for the work item.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Parent work item ID (for sub-tasks).
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// Description of the work to be done.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// List of files involved in this work item.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    /// Role of the agent assigned to this work item.
    /// </summary>
    public string? AssignedAgent { get; init; }
}
