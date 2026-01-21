namespace Apmas.Server.Core.Models;

/// <summary>
/// Result of spawning an agent process.
/// </summary>
public record SpawnResult
{
    /// <summary>
    /// Task ID assigned to the spawned agent.
    /// </summary>
    public required string TaskId { get; init; }

    /// <summary>
    /// Process ID of the spawned agent.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    /// Indicates whether the spawn operation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the spawn failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful spawn result.
    /// </summary>
    public static SpawnResult Succeeded(string taskId, int processId) =>
        new()
        {
            TaskId = taskId,
            ProcessId = processId,
            Success = true,
            ErrorMessage = null
        };

    /// <summary>
    /// Creates a failed spawn result.
    /// </summary>
    public static SpawnResult Failed(string taskId, string errorMessage) =>
        new()
        {
            TaskId = taskId,
            ProcessId = 0,
            Success = false,
            ErrorMessage = errorMessage
        };
}
