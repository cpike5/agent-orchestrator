using Apmas.Server.Agents.Definitions;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apmas.Server.Tests.Core.Services;

public class TimeoutHandlerTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly AgentStateManager _agentStateManager;
    private readonly FakeMessageBus _messageBus;
    private readonly TimeoutHandler _handler;
    private readonly IMemoryCache _cache;
    private readonly ApmasOptions _apmasOptions;

    public TimeoutHandlerTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);
        _cache = new MemoryCache(new MemoryCacheOptions());

        _apmasOptions = new ApmasOptions
        {
            ProjectName = "TestProject",
            WorkingDirectory = "/test/project",
            Timeouts = new TimeoutOptions
            {
                MaxRetries = 3,
                HeartbeatTimeoutMinutes = 10
            },
            Agents = new AgentOptions
            {
                Roster = new List<AgentDefinition>
                {
                    new() { Role = "test-agent", SubagentType = "general-purpose" }
                }
            }
        };

        var optionsWrapper = Options.Create(_apmasOptions);
        var agentOptionsWrapper = Options.Create(_apmasOptions.Agents);
        var roster = new AgentRoster(agentOptionsWrapper);

        var briefLoader = new FakeBriefLoader();
        _agentStateManager = new AgentStateManager(
            _stateStore,
            NullLogger<AgentStateManager>.Instance,
            _cache,
            optionsWrapper,
            roster,
            briefLoader);
        _messageBus = new FakeMessageBus();
        var dashboardPublisher = new FakeDashboardEventPublisher();

        _handler = new TimeoutHandler(
            _agentStateManager,
            _stateStore,
            _messageBus,
            dashboardPublisher,
            NullLogger<TimeoutHandler>.Instance,
            optionsWrapper);

        // Ensure database is created
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
        _cache.Dispose();
    }

    [Fact]
    public async Task HandleTimeoutAsync_FirstTimeout_SetsStatusToQueuedWithCheckpoint()
    {
        // Arrange - Create an agent with RetryCount 0
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            RetryCount = 0
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Create a checkpoint
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Designing database schema",
            CompletedTaskCount = 2,
            TotalTaskCount = 5,
            PendingItemsJson = "[\"Task 3\", \"Task 4\", \"Task 5\"]",
            CompletedItemsJson = "[\"Task 1\", \"Task 2\"]"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        await _handler.HandleTimeoutAsync("architect", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("architect");
        Assert.NotNull(updatedAgent);
        Assert.Equal(AgentStatus.Queued, updatedAgent.Status);
        Assert.Equal(1, updatedAgent.RetryCount);
        Assert.NotNull(updatedAgent.RecoveryContext);
        Assert.Contains("Recovery Context", updatedAgent.RecoveryContext);
        Assert.Contains("Designing database schema", updatedAgent.RecoveryContext);
        Assert.DoesNotContain("Reduced Scope", updatedAgent.RecoveryContext);
    }

    [Fact]
    public async Task HandleTimeoutAsync_FirstTimeout_NoCheckpoint_SetsStatusToQueuedWithoutRecoveryContext()
    {
        // Arrange - Create an agent with RetryCount 0 and no checkpoint
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running,
            RetryCount = 0
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act
        await _handler.HandleTimeoutAsync("developer", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("developer");
        Assert.NotNull(updatedAgent);
        Assert.Equal(AgentStatus.Queued, updatedAgent.Status);
        Assert.Equal(1, updatedAgent.RetryCount);
        Assert.Null(updatedAgent.RecoveryContext);
    }

    [Fact]
    public async Task HandleTimeoutAsync_SecondTimeout_SetsStatusToQueuedWithReducedScope()
    {
        // Arrange - Create an agent with RetryCount 1 (second timeout)
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            RetryCount = 1
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Create a checkpoint
        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Working on API endpoints",
            CompletedTaskCount = 3,
            TotalTaskCount = 5,
            PendingItemsJson = "[\"Task 4\", \"Task 5\"]"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        await _handler.HandleTimeoutAsync("architect", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("architect");
        Assert.NotNull(updatedAgent);
        Assert.Equal(AgentStatus.Queued, updatedAgent.Status);
        Assert.Equal(2, updatedAgent.RetryCount);
        Assert.NotNull(updatedAgent.RecoveryContext);
        Assert.Contains("Reduced Scope Mode", updatedAgent.RecoveryContext);
        Assert.Contains("smallest possible atomic tasks", updatedAgent.RecoveryContext);
    }

    [Fact]
    public async Task HandleTimeoutAsync_SecondTimeout_NoCheckpoint_SetsReducedScopeInstructions()
    {
        // Arrange - Create an agent with RetryCount 1 and no checkpoint
        var agent = new AgentState
        {
            Role = "tester",
            SubagentType = "test-runner",
            Status = AgentStatus.Running,
            RetryCount = 1
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act
        await _handler.HandleTimeoutAsync("tester", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("tester");
        Assert.NotNull(updatedAgent);
        Assert.Equal(AgentStatus.Queued, updatedAgent.Status);
        Assert.Equal(2, updatedAgent.RetryCount);
        Assert.NotNull(updatedAgent.RecoveryContext);
        Assert.Contains("Reduced Scope Mode", updatedAgent.RecoveryContext);
    }

    [Fact]
    public async Task HandleTimeoutAsync_ThirdTimeout_Escalates()
    {
        // Arrange - Create an agent with RetryCount 2 (third timeout, MaxRetries is 3)
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            RetryCount = 2
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act
        await _handler.HandleTimeoutAsync("architect", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("architect");
        Assert.NotNull(updatedAgent);
        Assert.Equal(AgentStatus.Escalated, updatedAgent.Status);
        Assert.Contains("escalated", updatedAgent.LastError?.ToLower() ?? "");
    }

    [Fact]
    public async Task HandleTimeoutAsync_ThirdTimeout_SendsNotification()
    {
        // Arrange - Create an agent that will escalate
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running,
            RetryCount = 2
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act
        await _handler.HandleTimeoutAsync("developer", CancellationToken.None);

        // Assert
        Assert.Single(_messageBus.PublishedMessages);
        var message = _messageBus.PublishedMessages[0];
        Assert.Equal("developer", message.From);
        Assert.Equal("supervisor", message.To);
        Assert.Equal(MessageType.Error, message.Type);
        Assert.Contains("ESCALATION", message.Content);
        Assert.Contains("developer", message.Content);
    }

    [Fact]
    public async Task HandleTimeoutAsync_Escalation_IncludesCheckpointProgressInMessage()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            RetryCount = 2
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var checkpoint = new Checkpoint
        {
            AgentRole = "architect",
            Summary = "Database schema design",
            CompletedTaskCount = 4,
            TotalTaskCount = 10
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        await _handler.HandleTimeoutAsync("architect", CancellationToken.None);

        // Assert
        var message = _messageBus.PublishedMessages[0];
        Assert.Contains("Database schema design", message.Content);
        Assert.Contains("4/10", message.Content);
    }

    [Fact]
    public async Task HandleTimeoutAsync_BuildsRecoveryContextWithCompletedItems()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running,
            RetryCount = 0
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var checkpoint = new Checkpoint
        {
            AgentRole = "developer",
            Summary = "Implementing user service",
            CompletedTaskCount = 2,
            TotalTaskCount = 4,
            CompletedItemsJson = "[\"Create user model\", \"Add repository\"]",
            PendingItemsJson = "[\"Add validation\", \"Write tests\"]",
            ActiveFilesJson = "[\"UserService.cs\", \"UserRepository.cs\"]",
            Notes = "Need to add input validation"
        };
        await _stateStore.SaveCheckpointAsync(checkpoint);

        // Act
        await _handler.HandleTimeoutAsync("developer", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("developer");
        Assert.NotNull(updatedAgent?.RecoveryContext);

        var context = updatedAgent.RecoveryContext;
        Assert.Contains("[x] Create user model", context);
        Assert.Contains("[x] Add repository", context);
        Assert.Contains("[ ] Add validation", context);
        Assert.Contains("[ ] Write tests", context);
        Assert.Contains("UserService.cs", context);
        Assert.Contains("Need to add input validation", context);
    }

    [Fact]
    public async Task HandleTimeoutAsync_ClearsTimeoutAt()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            RetryCount = 0,
            TimeoutAt = DateTime.UtcNow.AddMinutes(10)
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act
        await _handler.HandleTimeoutAsync("architect", CancellationToken.None);

        // Assert
        var updatedAgent = await _stateStore.GetAgentStateAsync("architect");
        Assert.Null(updatedAgent?.TimeoutAt);
    }

    [Fact]
    public async Task HandleTimeoutAsync_IncrementsRetryCountCorrectly()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running,
            RetryCount = 0
        };
        await _stateStore.SaveAgentStateAsync(agent);

        // Act - First timeout
        await _handler.HandleTimeoutAsync("developer", CancellationToken.None);
        var afterFirst = await _stateStore.GetAgentStateAsync("developer");
        Assert.Equal(1, afterFirst?.RetryCount);

        // Simulate agent running again
        await _agentStateManager.UpdateAgentStateAsync("developer", a =>
        {
            a.Status = AgentStatus.Running;
            return a;
        });

        // Act - Second timeout
        await _handler.HandleTimeoutAsync("developer", CancellationToken.None);
        var afterSecond = await _stateStore.GetAgentStateAsync("developer");
        Assert.Equal(2, afterSecond?.RetryCount);
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

    private class FakeMessageBus : IMessageBus
    {
        public List<AgentMessage> PublishedMessages { get; } = new();

        public Task PublishAsync(AgentMessage message)
        {
            PublishedMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AgentMessage>> GetMessagesForAgentAsync(string agentRole, DateTime? since = null)
        {
            return Task.FromResult<IReadOnlyList<AgentMessage>>(new List<AgentMessage>());
        }

        public Task<IReadOnlyList<AgentMessage>> GetAllMessagesAsync(int? limit = null)
        {
            return Task.FromResult<IReadOnlyList<AgentMessage>>(PublishedMessages.AsReadOnly());
        }

        public async IAsyncEnumerable<AgentMessage> SubscribeAsync(string? agentRole = null, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private class FakeDashboardEventPublisher : IDashboardEventPublisher
    {
        public Task PublishAgentUpdateAsync(AgentState agentState) => Task.CompletedTask;
        public Task PublishMessageAsync(AgentMessage message) => Task.CompletedTask;
        public Task PublishCheckpointAsync(Checkpoint checkpoint) => Task.CompletedTask;
        public Task PublishProjectUpdateAsync(ProjectState projectState) => Task.CompletedTask;
        public async IAsyncEnumerable<DashboardEvent> SubscribeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private class FakeBriefLoader : IProjectBriefLoader
    {
        public Task<string?> LoadBriefAsync(string workingDirectory, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }
}
