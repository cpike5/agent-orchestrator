using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Service for decomposing large tasks into smaller, manageable subtasks.
/// </summary>
public class TaskDecomposerService : ITaskDecomposerService
{
    private readonly ILogger<TaskDecomposerService> _logger;
    private readonly DecompositionOptions _options;

    public TaskDecomposerService(
        ILogger<TaskDecomposerService> logger,
        IOptions<ApmasOptions> options)
    {
        _logger = logger;
        _options = options.Value.Decomposition;
    }

    /// <inheritdoc />
    public int EstimateTaskTokens(WorkItem task)
    {
        var estimate = task.Files.Count * _options.TokensPerFile;

        _logger.LogDebug(
            "Estimated tokens for task {TaskId}: {EstimatedTokens} ({FileCount} files × {TokensPerFile} tokens/file)",
            task.Id,
            estimate,
            task.Files.Count,
            _options.TokensPerFile);

        return estimate;
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkItem> DecomposeTask(WorkItem task)
    {
        var estimatedTokens = EstimateTaskTokens(task);

        // If task is within safe context size, return it unchanged
        if (estimatedTokens <= _options.SafeContextTokens)
        {
            _logger.LogInformation(
                "Task {TaskId} is within safe context size ({EstimatedTokens} <= {SafeContextTokens} tokens) - no decomposition needed",
                task.Id,
                estimatedTokens,
                _options.SafeContextTokens);

            return new[] { task };
        }

        // Calculate maximum files per subtask
        var maxFilesPerSubtask = Math.Max(1, _options.SafeContextTokens / _options.TokensPerFile);

        _logger.LogInformation(
            "Task {TaskId} exceeds safe context size ({EstimatedTokens} > {SafeContextTokens} tokens) - decomposing into subtasks (max {MaxFilesPerSubtask} files per subtask)",
            task.Id,
            estimatedTokens,
            _options.SafeContextTokens,
            maxFilesPerSubtask);

        // Split files into batches
        var subtasks = new List<WorkItem>();
        var fileGroups = SplitFileIntoGroups(task.Files, maxFilesPerSubtask);
        var totalParts = fileGroups.Count;

        for (var i = 0; i < fileGroups.Count; i++)
        {
            var subtask = CreateSubtask(task, fileGroups[i], i + 1, totalParts);
            subtasks.Add(subtask);

            _logger.LogDebug(
                "Created subtask {SubtaskId} with {FileCount} files (Part {PartNumber} of {TotalParts})",
                subtask.Id,
                subtask.Files.Count,
                i + 1,
                totalParts);
        }

        _logger.LogInformation(
            "Task {TaskId} decomposed into {SubtaskCount} subtasks",
            task.Id,
            subtasks.Count);

        return subtasks;
    }

    /// <summary>
    /// Splits files into groups of the specified batch size.
    /// </summary>
    private List<List<string>> SplitFileIntoGroups(IReadOnlyList<string> files, int batchSize)
    {
        var groups = new List<List<string>>();

        for (var i = 0; i < files.Count; i += batchSize)
        {
            var batch = files
                .Skip(i)
                .Take(batchSize)
                .ToList();

            groups.Add(batch);
        }

        return groups;
    }

    /// <summary>
    /// Creates a subtask from a parent task and file batch.
    /// </summary>
    private WorkItem CreateSubtask(WorkItem parent, List<string> files, int partNumber, int totalParts)
    {
        return parent with
        {
            Id = $"{parent.Id}-{partNumber}",
            ParentId = parent.Id,
            Description = $"{parent.Description} (Part {partNumber} of {totalParts})",
            Files = files
        };
    }
}
