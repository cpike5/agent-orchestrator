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

namespace Apmas.Server.Tests.Mcp.Tools;

public class GetContextToolTests : IDisposable
{
    private readonly IDbContextFactory<ApmasDbContext> _contextFactory;
    private readonly SqliteStateStore _stateStore;
    private readonly AgentStateManager _stateManager;
    private readonly MessageBus _messageBus;
    private readonly GetContextTool _tool;

    public GetContextToolTests()
    {
        var options = new DbContextOptionsBuilder<ApmasDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _stateStore = new SqliteStateStore(_contextFactory, NullLogger<SqliteStateStore>.Instance);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var apmasOptions = new ApmasOptions
        {
            ProjectName = "TestProject",
            WorkingDirectory = "/test/project",
            Agents = new AgentOptions
            {
                Roster = new List<AgentDefinition>
                {
                    new() { Role = "test-agent", SubagentType = "general-purpose" }
                }
            }
        };
        var optionsWrapper = Options.Create(apmasOptions);
        var agentOptionsWrapper = Options.Create(apmasOptions.Agents);
        var roster = new AgentRoster(agentOptionsWrapper);

        var briefLoader = new FakeBriefLoader();
        _stateManager = new AgentStateManager(
            _stateStore,
            NullLogger<AgentStateManager>.Instance,
            cache,
            optionsWrapper,
            roster,
            briefLoader);
        var fakeMetrics = new FakeApmasMetrics();
        _messageBus = new MessageBus(_stateStore, fakeMetrics, NullLogger<MessageBus>.Instance);

        _tool = new GetContextTool(_stateManager, _messageBus, _stateStore, NullLogger<GetContextTool>.Instance);

        // Ensure database is created
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureCreated();

        // Initialize project state
        _stateManager.InitializeProjectAsync("TestProject", "/test/project").Wait();
    }

    public void Dispose()
    {
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        Assert.Equal("apmas_get_context", _tool.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_tool.Description));
    }

    [Fact]
    public void InputSchema_HasCorrectStructure()
    {
        var schema = _tool.InputSchema;

        Assert.Equal("object", schema["type"]?.ToString());
        Assert.NotNull(schema["properties"]);

        var properties = schema["properties"]?.AsObject();
        Assert.NotNull(properties);
        Assert.True(properties.ContainsKey("include"));
        Assert.True(properties.ContainsKey("agentRoles"));
        Assert.True(properties.ContainsKey("messageLimit"));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoParameters_ReturnsAllContext()
    {
        // Arrange - Add some test data
        await SetupTestDataAsync();

        var input = JsonDocument.Parse("{}").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("project", out _));
        Assert.True(response.RootElement.TryGetProperty("agents", out _));
        Assert.True(response.RootElement.TryGetProperty("messages", out _));
        Assert.True(response.RootElement.TryGetProperty("artifacts", out _));
    }

    [Fact]
    public async Task ExecuteAsync_WithProjectInclude_ReturnsProjectState()
    {
        // Arrange
        var input = JsonDocument.Parse(@"{
            ""include"": [""project""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("project", out var project));

        Assert.Equal("TestProject", project.GetProperty("name").GetString());
        Assert.Equal("/test/project", project.GetProperty("workingDirectory").GetString());
        Assert.NotNull(project.GetProperty("phase").GetString());
        Assert.NotNull(project.GetProperty("startedAt").GetString());

        // Should not include other sections
        Assert.False(response.RootElement.TryGetProperty("agents", out _));
        Assert.False(response.RootElement.TryGetProperty("messages", out _));
        Assert.False(response.RootElement.TryGetProperty("artifacts", out _));
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentsInclude_ReturnsAgentStates()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""agents""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("agents", out var agents));

        var agentsArray = agents.EnumerateArray().ToList();
        Assert.True(agentsArray.Count >= 2);

        var architect = agentsArray.First(a => a.GetProperty("role").GetString() == "architect");
        Assert.Equal("Running", architect.GetProperty("status").GetString());
        Assert.Equal("Designing system", architect.GetProperty("lastMessage").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_WithAgentRolesFilter_ReturnsFilteredAgents()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""agents""],
            ""agentRoles"": [""architect""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("agents", out var agents));

        var agentsArray = agents.EnumerateArray().ToList();
        Assert.Single(agentsArray);
        Assert.Equal("architect", agentsArray[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_WithMessagesInclude_ReturnsMessages()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""messages""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("messages", out var messages));

        var messagesArray = messages.EnumerateArray().ToList();
        Assert.True(messagesArray.Count >= 2);

        var firstMessage = messagesArray[0];
        Assert.NotNull(firstMessage.GetProperty("from").GetString());
        Assert.NotNull(firstMessage.GetProperty("to").GetString());
        Assert.NotNull(firstMessage.GetProperty("type").GetString());
        Assert.NotNull(firstMessage.GetProperty("content").GetString());
        Assert.NotNull(firstMessage.GetProperty("timestamp").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_WithMessageLimit_RespectsLimit()
    {
        // Arrange
        await SetupTestDataAsync();

        // Add more messages
        for (int i = 0; i < 60; i++)
        {
            await _messageBus.PublishAsync(new AgentMessage
            {
                Id = Guid.NewGuid().ToString(),
                From = "test-agent",
                To = "supervisor",
                Type = MessageType.Progress,
                Content = $"Message {i}"
            });
        }

        var input = JsonDocument.Parse(@"{
            ""include"": [""messages""],
            ""messageLimit"": 10
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("messages", out var messages));

        var messagesArray = messages.EnumerateArray().ToList();
        Assert.Equal(10, messagesArray.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WithArtifactsInclude_ReturnsArtifacts()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""artifacts""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("artifacts", out var artifacts));

        var artifactsArray = artifacts.EnumerateArray().ToList();
        Assert.True(artifactsArray.Count >= 2);

        var artifactPaths = artifactsArray.Select(a => a.GetString()).ToList();
        Assert.Contains("docs/architecture.md", artifactPaths);
        Assert.Contains("src/Program.cs", artifactPaths);
    }

    [Fact]
    public async Task ExecuteAsync_WithArtifactsAndAgentRoles_ReturnsFilteredArtifacts()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""artifacts""],
            ""agentRoles"": [""architect""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("artifacts", out var artifacts));

        var artifactsArray = artifacts.EnumerateArray().ToList();
        Assert.Single(artifactsArray);
        Assert.Equal("docs/architecture.md", artifactsArray[0].GetString());
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleIncludes_ReturnsAllRequestedSections()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""project"", ""agents""]
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert
        Assert.False(result.IsError);

        var response = JsonDocument.Parse(result.Content[0].Text!);
        Assert.True(response.RootElement.TryGetProperty("project", out _));
        Assert.True(response.RootElement.TryGetProperty("agents", out _));
        Assert.False(response.RootElement.TryGetProperty("messages", out _));
        Assert.False(response.RootElement.TryGetProperty("artifacts", out _));
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidMessageLimit_UsesDefault()
    {
        // Arrange
        await SetupTestDataAsync();

        var input = JsonDocument.Parse(@"{
            ""include"": [""messages""],
            ""messageLimit"": -1
        }").RootElement;

        // Act
        var result = await _tool.ExecuteAsync(input, CancellationToken.None);

        // Assert - should not error, just use default
        Assert.False(result.IsError);
    }

    private async Task SetupTestDataAsync()
    {
        // Add agent states
        var architect = new AgentState
        {
            Role = "architect",
            SubagentType = "systems-architect",
            Status = AgentStatus.Running,
            LastMessage = "Designing system",
            SpawnedAt = DateTime.UtcNow.AddMinutes(-30),
            ArtifactsJson = JsonSerializer.Serialize(new[] { "docs/architecture.md" })
        };
        await _stateStore.SaveAgentStateAsync(architect);

        var developer = new AgentState
        {
            Role = "developer",
            SubagentType = "backend-developer",
            Status = AgentStatus.Completed,
            LastMessage = "Implementation done",
            SpawnedAt = DateTime.UtcNow.AddMinutes(-20),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5),
            ArtifactsJson = JsonSerializer.Serialize(new[] { "src/Program.cs" })
        };
        await _stateStore.SaveAgentStateAsync(developer);

        // Add messages
        await _messageBus.PublishAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "architect",
            To = "supervisor",
            Type = MessageType.Done,
            Content = "Architecture complete"
        });

        await _messageBus.PublishAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            From = "developer",
            To = "architect",
            Type = MessageType.Question,
            Content = "Need clarification on design"
        });
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

    private class FakeBriefLoader : IProjectBriefLoader
    {
        public Task<string?> LoadBriefAsync(string workingDirectory, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }
}
