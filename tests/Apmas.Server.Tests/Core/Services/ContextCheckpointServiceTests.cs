using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Apmas.Server.Tests.Core.Services;

public class ContextCheckpointServiceTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly ContextCheckpointService _service;

    public ContextCheckpointServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);
        var fakeMetrics = new FakeApmasMetrics();
        _service = new ContextCheckpointService(_stateStore, fakeMetrics, NullLogger<ContextCheckpointService>.Instance);

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
    public async Task SaveCheckpointAsync_SavesCheckpointSuccessfully()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Designing database schema",
            CompletedTaskCount = 2,
            TotalTaskCount = 5
        };

        // Act
        await _service.SaveCheckpointAsync("architect", checkpoint);

        // Assert
        var saved = await _stateStore.GetLatestCheckpointAsync("architect");
        Assert.NotNull(saved);
        Assert.Equal("Designing database schema", saved.Summary);
        Assert.Equal(2, saved.CompletedTaskCount);
        Assert.Equal(5, saved.TotalTaskCount);
    }

    [Fact]
    public async Task SaveCheckpointAsync_CorrectsMismatchedAgentRole()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "wrong-role",
            Summary = "Test summary"
        };

        // Act
        await _service.SaveCheckpointAsync("correct-role", checkpoint);

        // Assert
        var saved = await _stateStore.GetLatestCheckpointAsync("correct-role");
        Assert.NotNull(saved);
        Assert.Equal("correct-role", saved.AgentRole);
    }

    [Fact]
    public async Task SaveCheckpointAsync_ThrowsOnNullAgentRole()
    {
        var checkpoint = new Checkpoint { AgentRole = "test", Summary = "Test" };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveCheckpointAsync(null!, checkpoint));
    }

    [Fact]
    public async Task SaveCheckpointAsync_ThrowsOnNullCheckpoint()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SaveCheckpointAsync("agent", null!));
    }

    [Fact]
    public async Task GetLatestCheckpointAsync_ReturnsLatestCheckpoint()
    {
        // Arrange
        var checkpoint1 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "First checkpoint",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var checkpoint2 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Second checkpoint",
            CreatedAt = DateTime.UtcNow
        };

        await _stateStore.SaveCheckpointAsync(checkpoint1);
        await _stateStore.SaveCheckpointAsync(checkpoint2);

        // Act
        var result = await _service.GetLatestCheckpointAsync("developer");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Second checkpoint", result.Summary);
    }

    [Fact]
    public async Task GetLatestCheckpointAsync_ReturnsNullWhenNoCheckpoints()
    {
        var result = await _service.GetLatestCheckpointAsync("nonexistent-agent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestCheckpointAsync_ThrowsOnNullAgentRole()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetLatestCheckpointAsync(null!));
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_ReturnsNullWhenNoCheckpoint()
    {
        var result = await _service.GenerateResumptionContextAsync("nonexistent-agent");
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_GeneratesExpectedFormat()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Designing API endpoints",
            CompletedTaskCount = 3,
            TotalTaskCount = 5,
            CompletedItemsJson = "[\"Task 1\", \"Task 2\", \"Task 3\"]",
            PendingItemsJson = "[\"Task 4\", \"Task 5\"]",
            Notes = "Need to consider rate limiting"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        var result = await _service.GenerateResumptionContextAsync("architect");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("## Previous Session Checkpoint", result);
        Assert.Contains("**Last Updated:**", result);
        Assert.Contains("### Summary", result);
        Assert.Contains("Designing API endpoints", result);
        Assert.Contains("### Progress: 60%", result);
        Assert.Contains("#### Completed:", result);
        Assert.Contains("[x] Task 1", result);
        Assert.Contains("[x] Task 2", result);
        Assert.Contains("[x] Task 3", result);
        Assert.Contains("#### Remaining:", result);
        Assert.Contains("[ ] Task 4", result);
        Assert.Contains("[ ] Task 5", result);
        Assert.Contains("### Notes", result);
        Assert.Contains("Need to consider rate limiting", result);
        Assert.Contains("**Continue from this checkpoint.**", result);
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_IncludesActiveFiles()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Working on service layer",
            CompletedTaskCount = 1,
            TotalTaskCount = 3,
            ActiveFilesJson = "[\"UserService.cs\", \"UserRepository.cs\"]"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        var result = await _service.GenerateResumptionContextAsync("developer");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("#### Active Files:", result);
        Assert.Contains("`UserService.cs`", result);
        Assert.Contains("`UserRepository.cs`", result);
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_HandlesEmptyCompletedItems()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Just starting",
            CompletedTaskCount = 0,
            TotalTaskCount = 5,
            CompletedItemsJson = null,
            PendingItemsJson = "[\"Task 1\", \"Task 2\"]"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        var result = await _service.GenerateResumptionContextAsync("architect");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("#### Completed:", result);
        Assert.Contains("- None", result);
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_HandlesEmptyNotes()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Working",
            Notes = null
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        var result = await _service.GenerateResumptionContextAsync("developer");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("### Notes", result);
        Assert.Contains("_No additional notes._", result);
    }

    [Fact]
    public async Task GenerateResumptionContextAsync_HandlesInvalidJsonGracefully()
    {
        // Arrange
        var checkpoint = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Test",
            CompletedItemsJson = "not valid json"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        var result = await _service.GenerateResumptionContextAsync("developer");

        // Assert - should include raw value instead of crashing
        Assert.NotNull(result);
        Assert.Contains("not valid json", result);
    }

    [Fact]
    public async Task GetCheckpointHistoryAsync_ReturnsCheckpointsInDescendingOrder()
    {
        // Arrange
        var checkpoint1 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "First",
            CreatedAt = DateTime.UtcNow.AddMinutes(-20)
        };
        var checkpoint2 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Second",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var checkpoint3 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Third",
            CreatedAt = DateTime.UtcNow
        };

        await _stateStore.SaveCheckpointAsync(checkpoint1);
        await _stateStore.SaveCheckpointAsync(checkpoint2);
        await _stateStore.SaveCheckpointAsync(checkpoint3);

        // Act
        var result = await _service.GetCheckpointHistoryAsync("developer");

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("Third", result[0].Summary);
        Assert.Equal("Second", result[1].Summary);
        Assert.Equal("First", result[2].Summary);
    }

    [Fact]
    public async Task GetCheckpointHistoryAsync_RespectsLimit()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await _stateStore.SaveCheckpointAsync(new Checkpoint
            {
                AgentRole = "developer",
                Summary = $"Checkpoint {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        // Act
        var result = await _service.GetCheckpointHistoryAsync("developer", limit: 3);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetCheckpointHistoryAsync_ReturnsEmptyListWhenNoCheckpoints()
    {
        var result = await _service.GetCheckpointHistoryAsync("nonexistent-agent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCheckpointHistoryAsync_ThrowsOnNullAgentRole()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetCheckpointHistoryAsync(null!));
    }

    [Fact]
    public async Task GetCheckpointHistoryAsync_FiltersToSpecificAgent()
    {
        // Arrange
        await _stateStore.SaveCheckpointAsync(new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Architect checkpoint"
        });
        await _stateStore.SaveCheckpointAsync(new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Developer checkpoint"
        });

        // Act
        var architectHistory = await _service.GetCheckpointHistoryAsync("architect");
        var developerHistory = await _service.GetCheckpointHistoryAsync("developer");

        // Assert
        Assert.Single(architectHistory);
        Assert.Equal("Architect checkpoint", architectHistory[0].Summary);
        Assert.Single(developerHistory);
        Assert.Equal("Developer checkpoint", developerHistory[0].Summary);
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

    private class FakeApmasMetrics : IApmasMetrics
    {
        public void RecordAgentSpawned(string role) { }
        public void RecordAgentCompleted(string role) { }
        public void RecordAgentFailed(string role, string reason) { }
        public void RecordAgentTimedOut(string role) { }
        public void RecordMessageSent(string messageType) { }
        public void RecordCheckpointSaved(string role) { }
        public void RecordAgentDuration(string role, double durationSeconds) { }
        public void RecordHeartbeatInterval(double intervalSeconds) { }
        public Task UpdateCachedMetricsAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}
