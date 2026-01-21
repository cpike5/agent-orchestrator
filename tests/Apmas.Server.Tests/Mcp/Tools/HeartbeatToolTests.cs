using System.Text.Json;
using Apmas.Server.Agents.Definitions;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Mcp.Tools;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Apmas.Server.Tests.Mcp.Tools;

public class HeartbeatToolTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly AgentStateManager _agentStateManager;
    private readonly FakeHeartbeatMonitor _heartbeatMonitor;
    private readonly HeartbeatTool _tool;
    private readonly IMemoryCache _cache;

    public HeartbeatToolTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);
        _cache = new MemoryCache(new MemoryCacheOptions());

        var apmasOptionsValue = new ApmasOptions
        {
            ProjectName = "TestProject",
            WorkingDirectory = "/test/project",
            Timeouts = new TimeoutOptions
            {
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
        var apmasOptions = Options.Create(apmasOptionsValue);
        var agentOptionsWrapper = Options.Create(apmasOptionsValue.Agents);
        var roster = new AgentRoster(agentOptionsWrapper);

        _agentStateManager = new AgentStateManager(
            _stateStore,
            NullLogger<AgentStateManager>.Instance,
            _cache,
            apmasOptions,
            roster);
        _heartbeatMonitor = new FakeHeartbeatMonitor();

        _tool = new HeartbeatTool(_agentStateManager, _heartbeatMonitor, NullLogger<HeartbeatTool>.Instance, apmasOptions);

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
    public void Name_ShouldReturnCorrectValue()
    {
        Assert.Equal("apmas_heartbeat", _tool.Name);
    }

    [Fact]
    public void Description_ShouldNotBeEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_tool.Description));
    }

    [Fact]
    public void InputSchema_ShouldRequireAgentRoleAndStatus()
    {
        var schema = _tool.InputSchema;

        Assert.NotNull(schema);
        Assert.Equal("object", schema["type"]?.ToString());

        var required = schema["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains(required, r => r?.ToString() == "agentRole");
        Assert.Contains(required, r => r?.ToString() == "status");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingAgentRole_ShouldReturnError()
    {
        var input = JsonDocument.Parse("""{"status": "working"}""").RootElement;

        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("agentRole", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingStatus_ShouldReturnError()
    {
        var input = JsonDocument.Parse("""{"agentRole": "architect"}""").RootElement;

        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("status", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidStatus_ShouldReturnError()
    {
        var input = JsonDocument.Parse("""{"agentRole": "architect", "status": "invalid"}""").RootElement;

        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("Invalid status", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ShouldUpdateAgentState()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""
        {
            "agentRole": "architect",
            "status": "working",
            "progress": "Designing system architecture",
            "estimatedContextUsage": 5000
        }
        """).RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains("acknowledged", result.Content[0].Text);

        var updatedAgent = await _stateStore.GetAgentStateAsync("architect");
        Assert.NotNull(updatedAgent);
        Assert.Equal("Designing system architecture", updatedAgent.LastMessage);
        Assert.Equal(5000, updatedAgent.EstimatedContextUsage);
        Assert.NotNull(updatedAgent.TimeoutAt);
        Assert.True(updatedAgent.TimeoutAt > DateTime.UtcNow.AddMinutes(9));
        Assert.True(updatedAgent.TimeoutAt < DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task ExecuteAsync_WithMinimalInput_ShouldUpdateTimeoutOnly()
    {
        // Arrange - Create an agent with existing message
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running,
            LastMessage = "Previous message"
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""
        {
            "agentRole": "developer",
            "status": "thinking"
        }
        """).RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var updatedAgent = await _stateStore.GetAgentStateAsync("developer");
        Assert.NotNull(updatedAgent);
        Assert.Equal("Previous message", updatedAgent.LastMessage); // Should not change
        Assert.NotNull(updatedAgent.TimeoutAt);
        Assert.True(updatedAgent.TimeoutAt > DateTime.UtcNow.AddMinutes(9));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAgentNotFound_ShouldReturnError()
    {
        var input = JsonDocument.Parse("""{"agentRole": "nonexistent", "status": "working"}""").RootElement;

        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("not found", result.Content[0].Text);
    }

    [Theory]
    [InlineData("working")]
    [InlineData("thinking")]
    [InlineData("writing")]
    public async Task ExecuteAsync_WithAllValidStatuses_ShouldSucceed(string status)
    {
        // Arrange - Create an agent
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse($$"""{"agentRole": "architect", "status": "{{status}}"}""").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldExtendTimeoutFromCurrentTime()
    {
        // Arrange - Create an agent with a timeout in the past
        var agent = new AgentState
        {
            Role = "reviewer",
            SubagentType = "code-reviewer",
            Status = AgentStatus.Running,
            TimeoutAt = DateTime.UtcNow.AddMinutes(-5) // In the past
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""{"agentRole": "reviewer", "status": "working"}""").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var updatedAgent = await _stateStore.GetAgentStateAsync("reviewer");
        Assert.NotNull(updatedAgent);
        Assert.NotNull(updatedAgent.TimeoutAt);
        // Should be 10 minutes from NOW, not from the old timeout
        Assert.True(updatedAgent.TimeoutAt > DateTime.UtcNow.AddMinutes(9));
        Assert.True(updatedAgent.TimeoutAt < DateTime.UtcNow.AddMinutes(11));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeProgressInResponse()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""
        {
            "agentRole": "architect",
            "status": "working",
            "progress": "Reviewing database schema"
        }
        """).RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var message = result.Content[0].Text;
        Assert.NotNull(message);
        Assert.Contains("Progress: Reviewing database schema", message);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordHeartbeatInMonitor()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""
        {
            "agentRole": "architect",
            "status": "working",
            "progress": "Designing system architecture"
        }
        """).RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Single(_heartbeatMonitor.RecordedHeartbeats);
        var heartbeat = _heartbeatMonitor.RecordedHeartbeats[0];
        Assert.Equal("architect", heartbeat.AgentRole);
        Assert.Equal("working", heartbeat.Status);
        Assert.Equal("Designing system architecture", heartbeat.Progress);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProgress_ShouldRecordHeartbeatWithNullProgress()
    {
        // Arrange
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse("""
        {
            "agentRole": "developer",
            "status": "thinking"
        }
        """).RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        Assert.Single(_heartbeatMonitor.RecordedHeartbeats);
        var heartbeat = _heartbeatMonitor.RecordedHeartbeats[0];
        Assert.Equal("developer", heartbeat.AgentRole);
        Assert.Equal("thinking", heartbeat.Status);
        Assert.Null(heartbeat.Progress);
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

    private class FakeHeartbeatMonitor : IHeartbeatMonitor
    {
        public List<HeartbeatRecord> RecordedHeartbeats { get; } = new();

        public void RecordHeartbeat(string agentRole, string status, string? progress)
        {
            RecordedHeartbeats.Add(new HeartbeatRecord(agentRole, status, progress));
        }

        public Task<bool> IsAgentHealthyAsync(string agentRole) => Task.FromResult(true);

        public Task<IReadOnlyList<string>> GetUnhealthyAgentsAsync() => Task.FromResult<IReadOnlyList<string>>(new List<string>());

        public void ClearAgent(string agentRole) { }

        public record HeartbeatRecord(string AgentRole, string Status, string? Progress);
    }
}
