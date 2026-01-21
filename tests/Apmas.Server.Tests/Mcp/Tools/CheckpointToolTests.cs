using System.Text.Json;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Core.Services;
using Apmas.Server.Mcp.Tools;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apmas.Server.Tests.Mcp.Tools;

public class CheckpointToolTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly AgentStateManager _agentStateManager;
    private readonly CheckpointTool _tool;
    private readonly IMemoryCache _cache;

    public CheckpointToolTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _agentStateManager = new AgentStateManager(_stateStore, NullLogger<AgentStateManager>.Instance, _cache);
        _tool = new CheckpointTool(_agentStateManager, _stateStore, NullLogger<CheckpointTool>.Instance);

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
    public void Name_ReturnsCorrectValue()
    {
        Assert.Equal("apmas_checkpoint", _tool.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_tool.Description));
    }

    [Fact]
    public void InputSchema_HasRequiredFields()
    {
        var schema = _tool.InputSchema;

        Assert.Equal("object", schema["type"]?.ToString());
        Assert.NotNull(schema["properties"]);
        Assert.NotNull(schema["required"]);

        var required = schema["required"]?.AsArray();
        Assert.NotNull(required);
        Assert.Contains(required, r => r?.ToString() == "agentRole");
        Assert.Contains(required, r => r?.ToString() == "summary");
        Assert.Contains(required, r => r?.ToString() == "completedItems");
        Assert.Contains(required, r => r?.ToString() == "pendingItems");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_SavesCheckpoint()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "developer-subagent",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""summary"": ""Completed feature A"",
            ""completedItems"": [""Task 1"", ""Task 2""],
            ""pendingItems"": [""Task 3"", ""Task 4""],
            ""activeFiles"": [""file1.cs"", ""file2.cs""],
            ""notes"": ""Need to refactor next""
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var savedCheckpoint = await _stateStore.GetLatestCheckpointAsync("developer");
        Assert.NotNull(savedCheckpoint);
        Assert.Equal("developer", savedCheckpoint.AgentRole);
        Assert.Equal("Completed feature A", savedCheckpoint.Summary);
        Assert.Equal(2, savedCheckpoint.CompletedTaskCount);
        Assert.Equal(4, savedCheckpoint.TotalTaskCount);
        Assert.Equal(50.0, savedCheckpoint.PercentComplete);
        Assert.NotNull(savedCheckpoint.CompletedItemsJson);
        Assert.NotNull(savedCheckpoint.PendingItemsJson);
        Assert.NotNull(savedCheckpoint.ActiveFilesJson);
        Assert.Equal("Need to refactor next", savedCheckpoint.Notes);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutOptionalFields_SavesCheckpoint()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "reviewer",
            SubagentType = "reviewer-subagent",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""reviewer"",
            ""summary"": ""Review in progress"",
            ""completedItems"": [""Review 1""],
            ""pendingItems"": [""Review 2"", ""Review 3"", ""Review 4""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var savedCheckpoint = await _stateStore.GetLatestCheckpointAsync("reviewer");
        Assert.NotNull(savedCheckpoint);
        Assert.Equal("reviewer", savedCheckpoint.AgentRole);
        Assert.Equal("Review in progress", savedCheckpoint.Summary);
        Assert.Equal(1, savedCheckpoint.CompletedTaskCount);
        Assert.Equal(4, savedCheckpoint.TotalTaskCount);
        Assert.Equal(25.0, savedCheckpoint.PercentComplete);
        Assert.Null(savedCheckpoint.ActiveFilesJson);
        Assert.Null(savedCheckpoint.Notes);
    }

    [Fact]
    public async Task ExecuteAsync_MissingAgentRole_ReturnsError()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""summary"": ""Some summary"",
            ""completedItems"": [],
            ""pendingItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("agentRole", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyAgentRole_ReturnsError()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""agentRole"": """",
            ""summary"": ""Some summary"",
            ""completedItems"": [],
            ""pendingItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("agentRole", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_MissingSummary_ReturnsError()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""completedItems"": [],
            ""pendingItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("summary", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCompletedItems_ReturnsError()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""summary"": ""Some summary"",
            ""pendingItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("completedItems", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPendingItems_ReturnsError()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""summary"": ""Some summary"",
            ""completedItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.True(result.IsError);
        Assert.Contains("pendingItems", result.Content[0].Text);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyArrays_SavesCheckpoint()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "architect",
            SubagentType = "architect-subagent",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""architect"",
            ""summary"": ""Planning phase"",
            ""completedItems"": [],
            ""pendingItems"": []
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var savedCheckpoint = await _stateStore.GetLatestCheckpointAsync("architect");
        Assert.NotNull(savedCheckpoint);
        Assert.Equal(0, savedCheckpoint.CompletedTaskCount);
        Assert.Equal(0, savedCheckpoint.TotalTaskCount);
        Assert.Equal(0, savedCheckpoint.PercentComplete);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccessMessage_WithPercentComplete()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "developer-subagent",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""summary"": ""Progress update"",
            ""completedItems"": [""A"", ""B"", ""C""],
            ""pendingItems"": [""D"", ""E"", ""F"", ""G""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);
        var message = result.Content[0].Text;
        Assert.NotNull(message);
        Assert.Contains("3/7", message);
        Assert.Contains("42.9%", message);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersOutEmptyStringsInArrays()
    {
        // Arrange - Create an agent first
        var agent = new AgentState
        {
            Role = "developer",
            SubagentType = "developer-subagent",
            Status = AgentStatus.Running
        };
        await _stateStore.SaveAgentStateAsync(agent);

        var input = JsonDocument.Parse(@"{
            ""agentRole"": ""developer"",
            ""summary"": ""Test filtering"",
            ""completedItems"": [""Task 1"", """", ""Task 2""],
            ""pendingItems"": [""Task 3"", """"],
            ""activeFiles"": [""file1.cs"", """", ""file2.cs""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var savedCheckpoint = await _stateStore.GetLatestCheckpointAsync("developer");
        Assert.NotNull(savedCheckpoint);

        var completedItems = JsonSerializer.Deserialize<string[]>(savedCheckpoint.CompletedItemsJson!);
        var pendingItems = JsonSerializer.Deserialize<string[]>(savedCheckpoint.PendingItemsJson!);
        var activeFiles = JsonSerializer.Deserialize<string[]>(savedCheckpoint.ActiveFilesJson!);

        Assert.Equal(2, completedItems?.Length);
        Assert.Equal(1, pendingItems?.Length);
        Assert.Equal(2, activeFiles?.Length);
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
