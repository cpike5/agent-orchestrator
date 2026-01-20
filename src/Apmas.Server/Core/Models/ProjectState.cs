using Apmas.Server.Core.Enums;

namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents the overall state of the project being managed.
/// </summary>
public class ProjectState
{
    /// <summary>
    /// Primary key for EF Core.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Name of the project.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Working directory for the project.
    /// </summary>
    public required string WorkingDirectory { get; set; }

    /// <summary>
    /// Current phase of the project.
    /// </summary>
    public ProjectPhase Phase { get; set; } = ProjectPhase.Initializing;

    /// <summary>
    /// When the project was started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the project completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
