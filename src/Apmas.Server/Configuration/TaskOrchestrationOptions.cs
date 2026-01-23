namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for task-based orchestration.
/// </summary>
public class TaskOrchestrationOptions
{
    /// <summary>
    /// Whether task-based orchestration is enabled.
    /// When false, the traditional role-based workflow is used.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Whether to spawn a reviewer after each phase completes.
    /// </summary>
    public bool ReviewAfterEachPhase { get; set; } = true;

    /// <summary>
    /// Whether to verify build succeeds after each task.
    /// </summary>
    public bool VerifyBuildAfterEachTask { get; set; } = true;

    /// <summary>
    /// Maximum number of tasks to run concurrently.
    /// Default is 1 for sequential execution.
    /// </summary>
    public int MaxConcurrentTasks { get; set; } = 1;

    /// <summary>
    /// The subagent type to use for task developers.
    /// </summary>
    public string DeveloperSubagentType { get; set; } = "dotnet-specialist";
}
