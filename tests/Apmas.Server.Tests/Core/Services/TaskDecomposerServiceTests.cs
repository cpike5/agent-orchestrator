using Apmas.Server.Configuration;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apmas.Server.Tests.Core.Services;

public class TaskDecomposerServiceTests
{
    private readonly TaskDecomposerService _decomposer;
    private readonly DecompositionOptions _options;

    public TaskDecomposerServiceTests()
    {
        _options = new DecompositionOptions
        {
            SafeContextTokens = 50_000,
            TokensPerFile = 15_000
        };

        var apmasOptions = new ApmasOptions
        {
            Decomposition = _options
        };

        var optionsWrapper = Options.Create(apmasOptions);

        _decomposer = new TaskDecomposerService(
            NullLogger<TaskDecomposerService>.Instance,
            optionsWrapper);
    }

    [Fact]
    public void EstimateTaskTokens_ReturnsCorrectEstimate()
    {
        // Arrange
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Implement user service",
            Files = new[] { "file1.cs", "file2.cs", "file3.cs" }
        };

        // Act
        var estimate = _decomposer.EstimateTaskTokens(task);

        // Assert
        Assert.Equal(45_000, estimate); // 3 files × 15,000 tokens/file
    }

    [Fact]
    public void DecomposeTask_SmallTask_ReturnsUnchanged()
    {
        // Arrange - Task with 3 files = 45,000 tokens (under 50,000 threshold)
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Implement user service",
            Files = new[] { "file1.cs", "file2.cs", "file3.cs" },
            AssignedAgent = "developer"
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.Single(result);
        Assert.Same(task, result[0]);
    }

    [Fact]
    public void DecomposeTask_LargeTask_SplitsIntoSubtasks()
    {
        // Arrange - Task with 10 files = 150,000 tokens (over 50,000 threshold)
        // SafeContextTokens / TokensPerFile = 50,000 / 15,000 = 3 files per subtask
        var files = Enumerable.Range(1, 10).Select(i => $"file{i}.cs").ToList();
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Implement user service",
            Files = files,
            AssignedAgent = "developer"
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert - Should be split into 4 subtasks (3+3+3+1 files)
        Assert.Equal(4, result.Count);

        // First subtask should have 3 files
        Assert.Equal(3, result[0].Files.Count);
        Assert.Equal("task-1-1", result[0].Id);
        Assert.Equal("task-1", result[0].ParentId);
        Assert.Equal("Implement user service (Part 1 of 4)", result[0].Description);
        Assert.Equal("developer", result[0].AssignedAgent);
        Assert.Contains("file1.cs", result[0].Files);
        Assert.Contains("file2.cs", result[0].Files);
        Assert.Contains("file3.cs", result[0].Files);

        // Second subtask should have 3 files
        Assert.Equal(3, result[1].Files.Count);
        Assert.Equal("task-1-2", result[1].Id);
        Assert.Equal("task-1", result[1].ParentId);
        Assert.Equal("Implement user service (Part 2 of 4)", result[1].Description);
        Assert.Contains("file4.cs", result[1].Files);
        Assert.Contains("file5.cs", result[1].Files);
        Assert.Contains("file6.cs", result[1].Files);

        // Third subtask should have 3 files
        Assert.Equal(3, result[2].Files.Count);
        Assert.Equal("task-1-3", result[2].Id);
        Assert.Equal("task-1", result[2].ParentId);
        Assert.Equal("Implement user service (Part 3 of 4)", result[2].Description);
        Assert.Contains("file7.cs", result[2].Files);
        Assert.Contains("file8.cs", result[2].Files);
        Assert.Contains("file9.cs", result[2].Files);

        // Fourth subtask should have 1 file (remainder)
        Assert.Single(result[3].Files);
        Assert.Equal("task-1-4", result[3].Id);
        Assert.Equal("task-1", result[3].ParentId);
        Assert.Equal("Implement user service (Part 4 of 4)", result[3].Description);
        Assert.Contains("file10.cs", result[3].Files);
    }

    [Fact]
    public void DecomposeTask_ExactlyAtThreshold_ReturnsUnchanged()
    {
        // Arrange - Task with exactly 3 files = 45,000 tokens (SafeContextTokens / TokensPerFile = 3)
        // Since estimate (45,000) is less than SafeContextTokens (50,000), should not decompose
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Implement user service",
            Files = new[] { "file1.cs", "file2.cs", "file3.cs" }
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.Single(result);
        Assert.Same(task, result[0]);
    }

    [Fact]
    public void DecomposeTask_JustOverThreshold_SplitsIntoTwo()
    {
        // Arrange - Task with 4 files = 60,000 tokens (just over 50,000 threshold)
        // Should split into 2 subtasks (3+1 files)
        var task = new WorkItem
        {
            Id = "task-2",
            Description = "Add authentication",
            Files = new[] { "file1.cs", "file2.cs", "file3.cs", "file4.cs" },
            AssignedAgent = "security-dev"
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result[0].Files.Count);
        Assert.Single(result[1].Files);
    }

    [Fact]
    public void DecomposeTask_SubtaskIdsReferenceParent()
    {
        // Arrange
        var files = Enumerable.Range(1, 7).Select(i => $"file{i}.cs").ToList();
        var task = new WorkItem
        {
            Id = "feature-123",
            Description = "Implement payment processing",
            Files = files
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.All(result, subtask =>
        {
            Assert.StartsWith("feature-123-", subtask.Id);
            Assert.Equal("feature-123", subtask.ParentId);
        });
    }

    [Fact]
    public void DecomposeTask_FileDistributionIsRoughlyEqual()
    {
        // Arrange - 10 files should be split into 4 subtasks (3+3+3+1)
        var files = Enumerable.Range(1, 10).Select(i => $"file{i}.cs").ToList();
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Large refactoring",
            Files = files
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert - First 3 subtasks should have 3 files each
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(3, result[i].Files.Count);
        }
        // Last subtask should have the remainder (1 file)
        Assert.Single(result[3].Files);

        // Verify all files are included exactly once
        var allFilesFromSubtasks = result.SelectMany(st => st.Files).ToList();
        Assert.Equal(10, allFilesFromSubtasks.Count);
        Assert.Equal(files.OrderBy(f => f), allFilesFromSubtasks.OrderBy(f => f));
    }

    [Fact]
    public void DecomposeTask_SingleFile_ReturnsUnchanged()
    {
        // Arrange
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Fix bug in AuthService",
            Files = new[] { "AuthService.cs" }
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.Single(result);
        Assert.Same(task, result[0]);
    }

    [Fact]
    public void DecomposeTask_EmptyFileList_ReturnsUnchanged()
    {
        // Arrange
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Research task",
            Files = Array.Empty<string>()
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.Single(result);
        Assert.Same(task, result[0]);
    }

    [Fact]
    public void DecomposeTask_PreservesAssignedAgent()
    {
        // Arrange
        var files = Enumerable.Range(1, 10).Select(i => $"file{i}.cs").ToList();
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Implement feature",
            Files = files,
            AssignedAgent = "backend-dev"
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        Assert.All(result, subtask => Assert.Equal("backend-dev", subtask.AssignedAgent));
    }

    [Fact]
    public void DecomposeTask_PartNumbersAreSequential()
    {
        // Arrange
        var files = Enumerable.Range(1, 10).Select(i => $"file{i}.cs").ToList();
        var task = new WorkItem
        {
            Id = "task-1",
            Description = "Big task",
            Files = files
        };

        // Act
        var result = _decomposer.DecomposeTask(task);

        // Assert
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Contains($"(Part {i + 1} of {result.Count})", result[i].Description);
        }
    }
}
