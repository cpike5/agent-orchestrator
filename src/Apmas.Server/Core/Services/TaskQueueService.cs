using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Implementation of the task queue service.
/// </summary>
public class TaskQueueService : ITaskQueueService
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<TaskQueueService> _logger;
    private readonly SemaphoreSlim _dequeueLock = new(1, 1);

    public TaskQueueService(IStateStore stateStore, ILogger<TaskQueueService> logger)
    {
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<bool> HasPendingTasksAsync()
    {
        var tasks = await _stateStore.GetTasksByStatusAsync(Enums.TaskStatus.Pending);
        return tasks.Count > 0;
    }

    public async Task<TaskItem?> DequeueNextTaskAsync()
    {
        await _dequeueLock.WaitAsync();
        try
        {
            var task = await _stateStore.GetNextPendingTaskAsync();
            if (task == null) return null;

            task.Status = Enums.TaskStatus.InProgress;
            task.StartedAt = DateTime.UtcNow;
            await _stateStore.SaveTaskAsync(task);

            _logger.LogInformation("Dequeued task {TaskId}: {Title}", task.TaskId, task.Title);
            return task;
        }
        finally
        {
            _dequeueLock.Release();
        }
    }

    public async Task SubmitTasksAsync(IEnumerable<TaskItem> tasks)
    {
        var taskList = tasks.ToList();
        var sequence = 1;
        foreach (var task in taskList)
        {
            if (task.SequenceNumber == 0)
            {
                task.SequenceNumber = sequence++;
            }
            await _stateStore.SaveTaskAsync(task);
            _logger.LogInformation("Submitted task {TaskId}: {Title}", task.TaskId, task.Title);
        }
        _logger.LogInformation("Submitted {Count} tasks to queue", taskList.Count);
    }

    public async Task UpdateTaskStatusAsync(string taskId, Enums.TaskStatus status, string? summary = null, string? error = null)
    {
        var task = await _stateStore.GetTaskAsync(taskId);
        if (task == null)
        {
            _logger.LogWarning("Cannot update status for unknown task {TaskId}", taskId);
            return;
        }

        task.Status = status;
        if (summary != null) task.ResultSummary = summary;
        if (error != null) task.ErrorMessage = error;
        if (status == Enums.TaskStatus.Completed) task.CompletedAt = DateTime.UtcNow;

        await _stateStore.SaveTaskAsync(task);
        _logger.LogInformation("Task {TaskId} status updated to {Status}", taskId, status);
    }

    public async Task<TaskItem?> GetCurrentTaskAsync()
    {
        var inProgress = await _stateStore.GetTasksByStatusAsync(Enums.TaskStatus.InProgress);
        return inProgress.FirstOrDefault();
    }

    public async Task<IReadOnlyList<TaskItem>> GetTaskQueueStatusAsync()
    {
        return await _stateStore.GetAllTasksAsync();
    }

    public async Task<bool> IsPhaseCompleteAsync(string phase)
    {
        return await _stateStore.AreAllTasksInPhaseCompletedAsync(phase);
    }

    public async Task<bool> AreAllTasksCompleteAsync()
    {
        var all = await _stateStore.GetAllTasksAsync();
        return all.Count > 0 && all.All(t => t.Status == Enums.TaskStatus.Completed);
    }
}
