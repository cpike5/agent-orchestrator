namespace Apmas.Server.Core.Models;

/// <summary>
/// Represents an event to be published to the dashboard.
/// </summary>
public record DashboardEvent
{
    /// <summary>
    /// Type of dashboard event.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Type-specific payload data.
    /// </summary>
    public required object Data { get; init; }
}

/// <summary>
/// Types of dashboard events.
/// </summary>
public static class DashboardEventTypes
{
    /// <summary>
    /// Agent status or metadata update.
    /// </summary>
    public const string AgentUpdate = "agent-update";

    /// <summary>
    /// Inter-agent or agent-supervisor message.
    /// </summary>
    public const string Message = "message";

    /// <summary>
    /// Agent progress checkpoint.
    /// </summary>
    public const string Checkpoint = "checkpoint";

    /// <summary>
    /// Project state update.
    /// </summary>
    public const string ProjectUpdate = "project-update";
}
