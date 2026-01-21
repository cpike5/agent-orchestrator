using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for decomposing large tasks into smaller, manageable subtasks.
/// </summary>
public interface ITaskDecomposerService
{
    /// <summary>
    /// Estimates the token count for a task based on file count.
    /// </summary>
    /// <param name="task">The work item to estimate.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTaskTokens(WorkItem task);

    /// <summary>
    /// Decomposes a task into smaller subtasks if it exceeds the safe context size.
    /// </summary>
    /// <param name="task">The work item to decompose.</param>
    /// <returns>A list containing either the original task (if small enough) or multiple subtasks.</returns>
    IReadOnlyList<WorkItem> DecomposeTask(WorkItem task);
}
