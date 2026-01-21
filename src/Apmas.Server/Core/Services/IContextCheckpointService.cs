using Apmas.Server.Core.Models;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Manages agent checkpoints and generates resumption context for agent recovery.
/// </summary>
public interface IContextCheckpointService
{
    /// <summary>
    /// Saves a checkpoint for an agent.
    /// </summary>
    /// <param name="agentRole">The role of the agent.</param>
    /// <param name="checkpoint">The checkpoint to save.</param>
    Task SaveCheckpointAsync(string agentRole, Checkpoint checkpoint);

    /// <summary>
    /// Gets the latest checkpoint for an agent.
    /// </summary>
    /// <param name="agentRole">The role of the agent.</param>
    /// <returns>The latest checkpoint, or null if none exists.</returns>
    Task<Checkpoint?> GetLatestCheckpointAsync(string agentRole);

    /// <summary>
    /// Generates markdown-formatted resumption context for an agent.
    /// </summary>
    /// <param name="agentRole">The role of the agent.</param>
    /// <returns>Formatted resumption context string, or null if no checkpoint exists.</returns>
    Task<string?> GenerateResumptionContextAsync(string agentRole);

    /// <summary>
    /// Gets the checkpoint history for an agent.
    /// </summary>
    /// <param name="agentRole">The role of the agent.</param>
    /// <param name="limit">Maximum number of checkpoints to return, or null for all.</param>
    /// <returns>List of checkpoints ordered by creation time descending.</returns>
    Task<IReadOnlyList<Checkpoint>> GetCheckpointHistoryAsync(string agentRole, int? limit = null);
}
