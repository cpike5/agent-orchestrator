using System.Text;
using System.Text.Json;
using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Manages agent checkpoints and generates resumption context for agent recovery.
/// </summary>
public class ContextCheckpointService : IContextCheckpointService
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<ContextCheckpointService> _logger;

    public ContextCheckpointService(
        IStateStore stateStore,
        ILogger<ContextCheckpointService> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SaveCheckpointAsync(string agentRole, Checkpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));
        ArgumentNullException.ThrowIfNull(checkpoint);

        // Ensure the checkpoint's agent role matches
        if (checkpoint.AgentRole != agentRole)
        {
            checkpoint.AgentRole = agentRole;
        }

        await _stateStore.SaveCheckpointAsync(checkpoint);

        _logger.LogInformation(
            "Saved checkpoint for agent {AgentRole}: {Summary} ({PercentComplete:F0}% complete)",
            agentRole,
            checkpoint.Summary,
            checkpoint.PercentComplete);
    }

    /// <inheritdoc />
    public async Task<Checkpoint?> GetLatestCheckpointAsync(string agentRole)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));

        var checkpoint = await _stateStore.GetLatestCheckpointAsync(agentRole);

        if (checkpoint != null)
        {
            _logger.LogDebug(
                "Retrieved latest checkpoint for agent {AgentRole}: {Summary}",
                agentRole,
                checkpoint.Summary);
        }
        else
        {
            _logger.LogDebug("No checkpoint found for agent {AgentRole}", agentRole);
        }

        return checkpoint;
    }

    /// <inheritdoc />
    public async Task<string?> GenerateResumptionContextAsync(string agentRole)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));

        var checkpoint = await GetLatestCheckpointAsync(agentRole);

        if (checkpoint == null)
        {
            _logger.LogDebug(
                "Cannot generate resumption context for agent {AgentRole}: no checkpoint exists",
                agentRole);
            return null;
        }

        var context = BuildResumptionContext(checkpoint);

        _logger.LogInformation(
            "Generated resumption context for agent {AgentRole} ({PercentComplete:F0}% complete)",
            agentRole,
            checkpoint.PercentComplete);

        return context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Checkpoint>> GetCheckpointHistoryAsync(string agentRole, int? limit = null)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));

        var history = await _stateStore.GetCheckpointHistoryAsync(agentRole, limit);

        _logger.LogDebug(
            "Retrieved {Count} checkpoints for agent {AgentRole}",
            history.Count,
            agentRole);

        return history;
    }

    /// <summary>
    /// Builds markdown-formatted resumption context from a checkpoint.
    /// </summary>
    private string BuildResumptionContext(Checkpoint checkpoint)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Previous Session Checkpoint");
        sb.AppendLine();
        sb.AppendLine($"**Last Updated:** {checkpoint.CreatedAt:u}");
        sb.AppendLine();

        // Summary section
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine(checkpoint.Summary);
        sb.AppendLine();

        // Progress section
        sb.AppendLine($"### Progress: {checkpoint.PercentComplete:F0}%");
        sb.AppendLine();

        // Completed items
        sb.AppendLine("#### Completed:");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(checkpoint.CompletedItemsJson))
        {
            try
            {
                var completedItems = JsonSerializer.Deserialize<List<string>>(checkpoint.CompletedItemsJson);
                if (completedItems != null && completedItems.Count > 0)
                {
                    foreach (var item in completedItems)
                    {
                        sb.AppendLine($"- [x] {item}");
                    }
                }
                else
                {
                    sb.AppendLine("- None");
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, include the raw value
                sb.AppendLine(checkpoint.CompletedItemsJson);
            }
        }
        else
        {
            sb.AppendLine("- None");
        }
        sb.AppendLine();

        // Remaining items
        sb.AppendLine("#### Remaining:");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(checkpoint.PendingItemsJson))
        {
            try
            {
                var pendingItems = JsonSerializer.Deserialize<List<string>>(checkpoint.PendingItemsJson);
                if (pendingItems != null && pendingItems.Count > 0)
                {
                    foreach (var item in pendingItems)
                    {
                        sb.AppendLine($"- [ ] {item}");
                    }
                }
                else
                {
                    sb.AppendLine("- None");
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, include the raw value
                sb.AppendLine(checkpoint.PendingItemsJson);
            }
        }
        else
        {
            sb.AppendLine("- None");
        }
        sb.AppendLine();

        // Active files (if present)
        if (!string.IsNullOrEmpty(checkpoint.ActiveFilesJson))
        {
            sb.AppendLine("#### Active Files:");
            sb.AppendLine();
            try
            {
                var activeFiles = JsonSerializer.Deserialize<List<string>>(checkpoint.ActiveFilesJson);
                if (activeFiles != null && activeFiles.Count > 0)
                {
                    foreach (var file in activeFiles)
                    {
                        sb.AppendLine($"- `{file}`");
                    }
                }
            }
            catch (JsonException)
            {
                sb.AppendLine(checkpoint.ActiveFilesJson);
            }
            sb.AppendLine();
        }

        // Notes section
        sb.AppendLine("### Notes");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(checkpoint.Notes))
        {
            sb.AppendLine(checkpoint.Notes);
        }
        else
        {
            sb.AppendLine("_No additional notes._");
        }
        sb.AppendLine();

        // Continuation instruction
        sb.AppendLine("**Continue from this checkpoint.**");

        return sb.ToString();
    }
}
