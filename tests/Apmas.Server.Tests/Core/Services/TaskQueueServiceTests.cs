using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = Apmas.Server.Core.Enums.TaskStatus;

namespace Apmas.Server.Tests.Core.Services;

public class TaskQueueServiceTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly TaskQueueService _service;

    public TaskQueueServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);
        _service = new TaskQueueService(_stateStore, NullLogger<TaskQueueService>.Instance);

        // Ensure database is created
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public async Task HasPendingTasksAsync_ReturnsTrueWhenPendingTasksExist()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.Pending
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        var result = await _service.HasPendingTasksAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasPendingTasksAsync_ReturnsFalseWhenNoPendingTasks()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.Completed
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        var result = await _service.HasPendingTasksAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task DequeueNextTaskAsync_ReturnsAndMarksTaskInProgress()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            SequenceNumber = 1,
            Status = TaskStatus.Pending
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        var result = await _service.DequeueNextTaskAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task-001", result.TaskId);
        Assert.Equal(TaskStatus.InProgress, result.Status);
        Assert.NotNull(result.StartedAt);
    }

    [Fact]
    public async Task DequeueNextTaskAsync_ReturnsNullWhenNoPendingTasks()
    {
        // Act
        var result = await _service.DequeueNextTaskAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DequeueNextTaskAsync_ReturnsTasksInSequenceOrder()
    {
        // Arrange
        var task1 = new TaskItem
        {
            TaskId = "task-001",
            Title = "Task 1",
            Description = "Description 1",
            SequenceNumber = 2,
            Status = TaskStatus.Pending
        };
        var task2 = new TaskItem
        {
            TaskId = "task-002",
            Title = "Task 2",
            Description = "Description 2",
            SequenceNumber = 1,
            Status = TaskStatus.Pending
        };
        await _stateStore.SaveTaskAsync(task1);
        await _stateStore.SaveTaskAsync(task2);

        // Act
        var result = await _service.DequeueNextTaskAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task-002", result.TaskId); // Lower sequence number should be first
    }

    [Fact]
    public async Task SubmitTasksAsync_SavesAllTasks()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1"
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2"
            }
        };

        // Act
        await _service.SubmitTasksAsync(tasks);

        // Assert
        var allTasks = await _stateStore.GetAllTasksAsync();
        Assert.Equal(2, allTasks.Count);
    }

    [Fact]
    public async Task SubmitTasksAsync_AssignsSequenceNumbersWhenNotSet()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1"
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2"
            }
        };

        // Act
        await _service.SubmitTasksAsync(tasks);

        // Assert
        var task1 = await _stateStore.GetTaskAsync("task-001");
        var task2 = await _stateStore.GetTaskAsync("task-002");
        Assert.Equal(1, task1!.SequenceNumber);
        Assert.Equal(2, task2!.SequenceNumber);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_UpdatesTaskStatus()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.Pending
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        await _service.UpdateTaskStatusAsync("task-001", TaskStatus.InProgress);

        // Assert
        var updated = await _stateStore.GetTaskAsync("task-001");
        Assert.NotNull(updated);
        Assert.Equal(TaskStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_UpdatesSummaryAndError()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.InProgress
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        await _service.UpdateTaskStatusAsync(
            "task-001",
            TaskStatus.Failed,
            summary: "Task failed",
            error: "Error details");

        // Assert
        var updated = await _stateStore.GetTaskAsync("task-001");
        Assert.NotNull(updated);
        Assert.Equal(TaskStatus.Failed, updated.Status);
        Assert.Equal("Task failed", updated.ResultSummary);
        Assert.Equal("Error details", updated.ErrorMessage);
    }

    [Fact]
    public async Task UpdateTaskStatusAsync_SetsCompletedAtWhenCompleted()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.InProgress
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        await _service.UpdateTaskStatusAsync("task-001", TaskStatus.Completed);

        // Assert
        var updated = await _stateStore.GetTaskAsync("task-001");
        Assert.NotNull(updated);
        Assert.Equal(TaskStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    [Fact]
    public async Task GetCurrentTaskAsync_ReturnsInProgressTask()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.InProgress
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        var result = await _service.GetCurrentTaskAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task-001", result.TaskId);
    }

    [Fact]
    public async Task GetCurrentTaskAsync_ReturnsNullWhenNoTaskInProgress()
    {
        // Arrange
        var task = new TaskItem
        {
            TaskId = "task-001",
            Title = "Test Task",
            Description = "Test Description",
            Status = TaskStatus.Pending
        };
        await _stateStore.SaveTaskAsync(task);

        // Act
        var result = await _service.GetCurrentTaskAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTaskQueueStatusAsync_ReturnsAllTasks()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1",
                Status = TaskStatus.Pending
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2",
                Status = TaskStatus.Completed
            }
        };
        await _service.SubmitTasksAsync(tasks);

        // Act
        var result = await _service.GetTaskQueueStatusAsync();

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task IsPhaseCompleteAsync_ReturnsTrueWhenAllPhaseTasksComplete()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1",
                Phase = "phase1",
                Status = TaskStatus.Completed
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2",
                Phase = "phase1",
                Status = TaskStatus.Completed
            }
        };
        await _service.SubmitTasksAsync(tasks);

        // Act
        var result = await _service.IsPhaseCompleteAsync("phase1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsPhaseCompleteAsync_ReturnsFalseWhenPhaseTasksPending()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1",
                Phase = "phase1",
                Status = TaskStatus.Completed
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2",
                Phase = "phase1",
                Status = TaskStatus.Pending
            }
        };
        await _service.SubmitTasksAsync(tasks);

        // Act
        var result = await _service.IsPhaseCompleteAsync("phase1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreAllTasksCompleteAsync_ReturnsTrueWhenAllComplete()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1",
                Status = TaskStatus.Completed
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2",
                Status = TaskStatus.Completed
            }
        };
        await _service.SubmitTasksAsync(tasks);

        // Act
        var result = await _service.AreAllTasksCompleteAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task AreAllTasksCompleteAsync_ReturnsFalseWhenTasksPending()
    {
        // Arrange
        var tasks = new[]
        {
            new TaskItem
            {
                TaskId = "task-001",
                Title = "Task 1",
                Description = "Description 1",
                Status = TaskStatus.Completed
            },
            new TaskItem
            {
                TaskId = "task-002",
                Title = "Task 2",
                Description = "Description 2",
                Status = TaskStatus.Pending
            }
        };
        await _service.SubmitTasksAsync(tasks);

        // Act
        var result = await _service.AreAllTasksCompleteAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task AreAllTasksCompleteAsync_ReturnsFalseWhenNoTasks()
    {
        // Act
        var result = await _service.AreAllTasksCompleteAsync();

        // Assert
        Assert.False(result);
    }

    private class TestDbContextFactory : IDbContextFactory<ApmasDbContext>
    {
        private readonly DbContextOptions<ApmasDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApmasDbContext> options)
        {
            _options = options;
        }

        public ApmasDbContext CreateDbContext()
        {
            return new ApmasDbContext(_options);
        }
    }
}
