using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents the current state of the project being managed.
/// </summary>
public record ProjectState
{
    /// <summary>
    /// Unique identifier for the project state record.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Name of the project.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Working directory for the project.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Current phase of the project.
    /// </summary>
    public required ProjectPhase Phase { get; init; }

    /// <summary>
    /// When the project was started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// When the project completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Dictionary of agent states keyed by role.
    /// </summary>
    public IReadOnlyDictionary<string, AgentState> Agents { get; init; } =
        new Dictionary<string, AgentState>();
}
