using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apmas.Server.Tests.Storage;

public class SqliteStateStoreTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _store;

    public SqliteStateStoreTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _store = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);

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
    public async Task SaveAndGetProjectState_Works()
    {
        var state = new ProjectState
        {
            Name = "test-project",
            WorkingDirectory = "/test",
            Phase = ProjectPhase.Building
        };

        await _store.SaveProjectStateAsync(state);
        var retrieved = await _store.GetProjectStateAsync();

        Assert.NotNull(retrieved);
        Assert.Equal("test-project", retrieved.Name);
        Assert.Equal("/test", retrieved.WorkingDirectory);
        Assert.Equal(ProjectPhase.Building, retrieved.Phase);
    }

    [Fact]
    public async Task SaveProjectState_UpdatesExisting()
    {
        var state = new ProjectState
        {
            Name = "test-project",
            WorkingDirectory = "/test",
            Phase = ProjectPhase.Initializing
        };

        await _store.SaveProjectStateAsync(state);

        var updated = new ProjectState
        {
            Name = "test-project",
            WorkingDirectory = "/test",
            Phase = ProjectPhase.Completed
        };

        await _store.SaveProjectStateAsync(updated);

        var retrieved = await _store.GetProjectStateAsync();
        Assert.NotNull(retrieved);
        Assert.Equal(ProjectPhase.Completed, retrieved.Phase);
    }

    [Fact]
    public async Task SaveAndGetAgentState_Works()
    {
        var state = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running
        };

        await _store.SaveAgentStateAsync(state);
        var retrieved = await _store.GetAgentStateAsync("architect");

        Assert.NotNull(retrieved);
        Assert.Equal("architect", retrieved.Role);
        Assert.Equal("systems-architect", retrieved.SubagentType);
        Assert.Equal(AgentStatus.Running, retrieved.Status);
    }

    [Fact]
    public async Task SaveAgentState_UpdatesExisting()
    {
        var state = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            Status = AgentStatus.Pending
        };

        await _store.SaveAgentStateAsync(state);

        var updated = new AgentState
        {
            Role = "developer",
            SubagentType = "dotnet-specialist",
            Status = AgentStatus.Completed,
            LastMessage = "All done"
        };

        await _store.SaveAgentStateAsync(updated);

        var retrieved = await _store.GetAgentStateAsync("developer");
        Assert.NotNull(retrieved);
        Assert.Equal(AgentStatus.Completed, retrieved.Status);
        Assert.Equal("All done", retrieved.LastMessage);
    }

    [Fact]
    public async Task GetAllAgentStates_ReturnsAll()
    {
        await _store.SaveAgentStateAsync(new AgentState { Role = "architect", SubagentType = "systems-architect" });
        await _store.SaveAgentStateAsync(new AgentState { Role = "developer", SubagentType = "dotnet-specialist" });
        await _store.SaveAgentStateAsync(new AgentState { Role = "reviewer", SubagentType = "code-reviewer" });

        var all = await _store.GetAllAgentStatesAsync();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task SaveAndGetLatestCheckpoint_Works()
    {
        var checkpoint1 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "First checkpoint",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedTaskCount = 2,
            TotalTaskCount = 5
        };

        var checkpoint2 = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Second checkpoint",
            CreatedAt = DateTime.UtcNow,
            CompletedTaskCount = 4,
            TotalTaskCount = 5
        };

        await _store.SaveCheckpointAsync(checkpoint1);
        await _store.SaveCheckpointAsync(checkpoint2);

        var latest = await _store.GetLatestCheckpointAsync("developer");

        Assert.NotNull(latest);
        Assert.Equal("Second checkpoint", latest.Summary);
        Assert.Equal(4, latest.CompletedTaskCount);
    }

    [Fact]
    public async Task GetLatestCheckpoint_ReturnsNullWhenNone()
    {
        var result = await _store.GetLatestCheckpointAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndGetMessage_Works()
    {
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "developer",
            To = "reviewer",
            Type = MessageType.NeedsReview,
            Content = "Please review my code"
        };

        await _store.SaveMessageAsync(message);
        var messages = await _store.GetMessagesAsync();

        Assert.Single(messages);
        Assert.Equal("developer", messages[0].From);
        Assert.Equal("reviewer", messages[0].To);
    }

    [Fact]
    public async Task GetMessages_FiltersByRole()
    {
        await _store.SaveMessageAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "developer",
            To = "reviewer",
            Type = MessageType.NeedsReview,
            Content = "Review please"
        });

        await _store.SaveMessageAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "architect",
            To = "supervisor",
            Type = MessageType.Done,
            Content = "Architecture complete"
        });

        var developerMessages = await _store.GetMessagesAsync(role: "developer");

        Assert.Single(developerMessages);
        Assert.Equal("developer", developerMessages[0].From);
    }

    [Fact]
    public async Task GetMessages_FiltersBySince()
    {
        var oldMessage = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "developer",
            To = "supervisor",
            Type = MessageType.Heartbeat,
            Content = "Old heartbeat",
            Timestamp = DateTime.UtcNow.AddHours(-2)
        };

        var newMessage = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "developer",
            To = "supervisor",
            Type = MessageType.Heartbeat,
            Content = "New heartbeat",
            Timestamp = DateTime.UtcNow
        };

        await _store.SaveMessageAsync(oldMessage);
        await _store.SaveMessageAsync(newMessage);

        var recentMessages = await _store.GetMessagesAsync(since: DateTime.UtcNow.AddHours(-1));

        Assert.Single(recentMessages);
        Assert.Equal("New heartbeat", recentMessages[0].Content);
    }

    [Fact]
    public async Task GetMessages_LimitsResults()
    {
        for (int i = 0; i < 10; i++)
        {
            await _store.SaveMessageAsync(new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = "developer",
                To = "supervisor",
                Type = MessageType.Heartbeat,
                Content = $"Message {i}"
            });
        }

        var limitedMessages = await _store.GetMessagesAsync(limit: 5);

        Assert.Equal(5, limitedMessages.Count);
    }

    [Fact]
    public async Task ConcurrentAccess_HandledSafely()
    {
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await _store.SaveMessageAsync(new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = $"agent-{i}",
                To = "supervisor",
                Type = MessageType.Heartbeat,
                Content = $"Heartbeat {i}"
            });
        });

        await Task.WhenAll(tasks);

        var allMessages = await _store.GetMessagesAsync();
        Assert.Equal(10, allMessages.Count);
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
