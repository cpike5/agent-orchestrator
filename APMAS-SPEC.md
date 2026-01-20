# APMAS: Autonomous Project Management Agent System

## .NET MCP Server Specification

A comprehensive specification for a multi-agent coordination system using a .NET MCP (Model Context Protocol) server as the central orchestration layer.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Core Components](#core-components)
4. [MCP Server Design](#mcp-server-design)
5. [Agent Lifecycle Management](#agent-lifecycle-management)
6. [Timeout Handling](#timeout-handling)
7. [Context Limit Management](#context-limit-management)
8. [Monitoring & Observability](#monitoring--observability)
9. [Data Models](#data-models)
10. [MCP Tools Specification](#mcp-tools-specification)
11. [Agent Prompts](#agent-prompts)
12. [Implementation Plan](#implementation-plan)

---

## Executive Summary

APMAS is a multi-agent coordination system that enables autonomous AI agents to collaborate on software projects. Based on lessons learned from the blog-prototype POC, this system addresses three critical challenges:

1. **Agent Timeouts**: Agents that hang or take too long
2. **Context Limits**: Agents exhausting their context window
3. **Monitoring & Restart**: Detecting failures and recovering gracefully

The key architectural insight: **Agents cannot self-coordinate reliably**. An external orchestrator (the MCP server) must manage agent lifecycle, dependencies, and communication.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      APMAS MCP Server (.NET 8)                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │ Supervisor  │  │   State     │  │  Message    │  │  Agent      │        │
│  │  Service    │  │  Manager    │  │   Bus       │  │  Spawner    │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
│         │                │                │                │                │
│         └────────────────┴────────────────┴────────────────┘                │
│                                    │                                        │
│  ┌─────────────────────────────────┴─────────────────────────────────┐     │
│  │                    MCP Tool Handlers                               │     │
│  │  • apmas_heartbeat    • apmas_report_status   • apmas_get_context │     │
│  │  • apmas_checkpoint   • apmas_request_help    • apmas_complete    │     │
│  └───────────────────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │ MCP Protocol        │ MCP Protocol        │ MCP Protocol
              ▼                     ▼                     ▼
    ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐
    │  Claude Agent   │   │  Claude Agent   │   │  Claude Agent   │
    │   (Architect)   │   │   (Developer)   │   │   (Reviewer)    │
    │                 │   │                 │   │                 │
    │ Uses MCP tools  │   │ Uses MCP tools  │   │ Uses MCP tools  │
    │ to communicate  │   │ to communicate  │   │ to communicate  │
    └─────────────────┘   └─────────────────┘   └─────────────────┘
              │                     │                     │
              ▼                     ▼                     ▼
    ┌─────────────────────────────────────────────────────────────┐
    │                    Project Directory                         │
    │  ├── .apmas/           (state, logs, checkpoints)           │
    │  ├── docs/             (documentation artifacts)            │
    │  ├── src/              (source code artifacts)              │
    │  └── tests/            (test artifacts)                     │
    └─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. Supervisor Service

The brain of APMAS. Runs as a background service that:

- Monitors agent heartbeats
- Detects timeouts and failures
- Manages agent lifecycle (spawn, restart, terminate)
- Enforces dependency ordering
- Escalates to humans when needed

```csharp
public class SupervisorService : BackgroundService
{
    private readonly IAgentStateManager _stateManager;
    private readonly IAgentSpawner _spawner;
    private readonly IMessageBus _messageBus;
    private readonly SupervisorOptions _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAgentHealthAsync();
            await ProcessPendingAgentsAsync();
            await CheckDependenciesAsync();

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }
}
```

### 2. State Manager

Maintains the single source of truth for project and agent state:

```csharp
public interface IAgentStateManager
{
    Task<ProjectState> GetProjectStateAsync();
    Task<AgentState> GetAgentStateAsync(string agentRole);
    Task UpdateAgentStateAsync(string agentRole, AgentState state);
    Task<IReadOnlyList<AgentState>> GetActiveAgentsAsync();
    Task<IReadOnlyList<string>> GetReadyAgentsAsync();
}
```

### 3. Message Bus

Handles inter-agent communication with guaranteed delivery:

```csharp
public interface IMessageBus
{
    Task PublishAsync(AgentMessage message);
    Task<IReadOnlyList<AgentMessage>> GetMessagesForAgentAsync(string agentRole, DateTime? since = null);
    Task<IReadOnlyList<AgentMessage>> GetAllMessagesAsync(int? limit = null);
    IAsyncEnumerable<AgentMessage> SubscribeAsync(string? agentRole = null, CancellationToken ct = default);
}
```

### 4. Agent Spawner

Launches Claude Code agents with proper configuration:

```csharp
public interface IAgentSpawner
{
    Task<SpawnResult> SpawnAgentAsync(AgentConfig config, string? additionalContext = null);
    Task<bool> TerminateAgentAsync(string agentRole);
    Task<AgentProcessInfo?> GetAgentProcessAsync(string agentRole);
}
```

---

## MCP Server Design

### Project Structure

```
Apmas.Server/
├── Apmas.Server.csproj
├── Program.cs
├── appsettings.json
│
├── Configuration/
│   ├── ApmasOptions.cs
│   ├── AgentDefinitions.cs
│   └── TimeoutPolicies.cs
│
├── Core/
│   ├── Services/
│   │   ├── SupervisorService.cs
│   │   ├── AgentStateManager.cs
│   │   ├── MessageBus.cs
│   │   ├── AgentSpawner.cs
│   │   └── ContextCheckpointService.cs
│   │
│   ├── Models/
│   │   ├── ProjectState.cs
│   │   ├── AgentState.cs
│   │   ├── AgentMessage.cs
│   │   ├── Checkpoint.cs
│   │   └── WorkItem.cs
│   │
│   └── Enums/
│       ├── AgentStatus.cs
│       ├── MessageType.cs
│       └── ProjectPhase.cs
│
├── Mcp/
│   ├── ApmasMcpServer.cs
│   ├── Tools/
│   │   ├── HeartbeatTool.cs
│   │   ├── ReportStatusTool.cs
│   │   ├── GetContextTool.cs
│   │   ├── CheckpointTool.cs
│   │   ├── RequestHelpTool.cs
│   │   ├── CompleteTool.cs
│   │   └── SendMessageTool.cs
│   │
│   └── Resources/
│       ├── ProjectStateResource.cs
│       ├── AgentMessagesResource.cs
│       └── CheckpointResource.cs
│
├── Agents/
│   ├── Prompts/
│   │   ├── BaseAgentPrompt.cs
│   │   ├── ProjectManagerPrompt.cs
│   │   ├── ArchitectPrompt.cs
│   │   ├── DeveloperPrompt.cs
│   │   ├── ReviewerPrompt.cs
│   │   └── TesterPrompt.cs
│   │
│   └── Definitions/
│       └── AgentRoster.cs
│
└── Storage/
    ├── IStateStore.cs
    ├── FileStateStore.cs
    └── SqliteStateStore.cs
```

### Dependency Injection Setup

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<ApmasOptions>(
    builder.Configuration.GetSection("Apmas"));

// Core Services
builder.Services.AddSingleton<IAgentStateManager, AgentStateManager>();
builder.Services.AddSingleton<IMessageBus, MessageBus>();
builder.Services.AddSingleton<IAgentSpawner, ClaudeCodeSpawner>();
builder.Services.AddSingleton<IContextCheckpointService, ContextCheckpointService>();
builder.Services.AddSingleton<IStateStore, SqliteStateStore>();

// Background Services
builder.Services.AddHostedService<SupervisorService>();
builder.Services.AddHostedService<McpServerHost>();

// Logging
builder.Services.AddSerilog(config => config
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .Enrich.WithProperty("Application", "APMAS"));

var host = builder.Build();
await host.RunAsync();
```

---

## Agent Lifecycle Management

### Agent States

```csharp
public enum AgentStatus
{
    Pending,        // Waiting for dependencies
    Queued,         // Dependencies met, ready to spawn
    Spawning,       // Being launched
    Running,        // Actively working
    Paused,         // Checkpointed, waiting to resume
    Completed,      // Successfully finished
    Failed,         // Failed (will retry)
    TimedOut,       // Exceeded time limit
    Escalated       // Requires human intervention
}
```

### State Transitions

```
                    ┌─────────────────────────────────────┐
                    │                                     │
                    ▼                                     │
┌─────────┐    ┌─────────┐    ┌─────────┐    ┌──────────┐│
│ Pending │───▶│ Queued  │───▶│Spawning │───▶│ Running  ││
└─────────┘    └─────────┘    └─────────┘    └──────────┘│
     ▲              │              │              │       │
     │              │              │              │       │
     │              ▼              ▼              ▼       │
     │         ┌─────────┐   ┌─────────┐    ┌─────────┐  │
     │         │ Failed  │   │ Failed  │    │ Paused  │──┘
     │         └─────────┘   └─────────┘    └─────────┘
     │              │              │              │
     │              └──────┬───────┘              │
     │                     ▼                      │
     │              ┌───────────┐                 │
     │              │ Escalated │                 │
     │              └───────────┘                 │
     │                                            │
     │    ┌───────────┐                          │
     └────│ Completed │◀─────────────────────────┘
          └───────────┘
                ▲
                │
          ┌───────────┐
          │ TimedOut  │ (retry → Queued, or escalate)
          └───────────┘
```

### Dependency Resolution

```csharp
public class DependencyResolver
{
    private readonly IAgentStateManager _stateManager;
    private readonly AgentRoster _roster;

    public async Task<IReadOnlyList<string>> GetReadyAgentsAsync()
    {
        var projectState = await _stateManager.GetProjectStateAsync();
        var readyAgents = new List<string>();

        foreach (var agent in _roster.Agents)
        {
            if (agent.Status != AgentStatus.Pending)
                continue;

            var dependencies = _roster.GetDependencies(agent.Role);
            var allDependenciesMet = true;

            foreach (var dep in dependencies)
            {
                var depState = await _stateManager.GetAgentStateAsync(dep);
                if (depState.Status != AgentStatus.Completed)
                {
                    allDependenciesMet = false;
                    break;
                }
            }

            if (allDependenciesMet)
            {
                readyAgents.Add(agent.Role);
            }
        }

        return readyAgents;
    }
}
```

---

## Timeout Handling

### Timeout Configuration

```csharp
public class TimeoutPolicy
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public int MaxRetries { get; set; } = 3;

    public Dictionary<string, TimeSpan> AgentTimeouts { get; set; } = new()
    {
        ["architect"] = TimeSpan.FromMinutes(15),
        ["designer"] = TimeSpan.FromMinutes(15),
        ["developer"] = TimeSpan.FromMinutes(45),
        ["tester"] = TimeSpan.FromMinutes(30),
        ["reviewer"] = TimeSpan.FromMinutes(20),
        ["docs"] = TimeSpan.FromMinutes(20)
    };
}
```

### Timeout Handler

```csharp
public class TimeoutHandler
{
    private readonly IAgentStateManager _stateManager;
    private readonly IAgentSpawner _spawner;
    private readonly IContextCheckpointService _checkpointService;
    private readonly ILogger<TimeoutHandler> _logger;
    private readonly TimeoutPolicy _policy;

    public async Task HandleTimeoutAsync(string agentRole)
    {
        var state = await _stateManager.GetAgentStateAsync(agentRole);
        state.RetryCount++;

        _logger.LogWarning(
            "Agent {Role} timed out (attempt {Attempt}/{Max})",
            agentRole, state.RetryCount, _policy.MaxRetries);

        if (state.RetryCount == 1)
        {
            // First timeout: restart with checkpoint
            await RestartWithCheckpointAsync(agentRole, state);
        }
        else if (state.RetryCount == 2)
        {
            // Second timeout: restart fresh with smaller scope
            await RestartWithReducedScopeAsync(agentRole, state);
        }
        else if (state.RetryCount >= _policy.MaxRetries)
        {
            // Max retries: escalate to human
            await EscalateToHumanAsync(agentRole, state);
        }
        else
        {
            // Additional retry: restart fresh
            await RestartFreshAsync(agentRole, state);
        }
    }

    private async Task RestartWithCheckpointAsync(string agentRole, AgentState state)
    {
        var checkpoint = await _checkpointService.GetLatestCheckpointAsync(agentRole);

        var contextInjection = $"""
            ## CONTINUATION CONTEXT

            You were previously working on this task but were interrupted.

            ### Your Previous Progress:
            {checkpoint?.Summary ?? "No checkpoint available"}

            ### Files You Created:
            {string.Join("\n", state.Artifacts.Select(a => $"- {a}"))}

            ### Last Status:
            {state.LastMessage}

            **Continue from where you left off.** Do not restart from scratch.
            """;

        await _spawner.SpawnAgentAsync(
            _roster.GetAgent(agentRole),
            additionalContext: contextInjection);

        await _stateManager.UpdateAgentStateAsync(agentRole, state with
        {
            Status = AgentStatus.Spawning,
            SpawnedAt = DateTime.UtcNow,
            TimeoutAt = DateTime.UtcNow + _policy.GetTimeoutFor(agentRole)
        });
    }

    private async Task EscalateToHumanAsync(string agentRole, AgentState state)
    {
        _logger.LogError(
            "Agent {Role} failed after {Attempts} attempts. Escalating to human.",
            agentRole, state.RetryCount);

        await _stateManager.UpdateAgentStateAsync(agentRole, state with
        {
            Status = AgentStatus.Escalated
        });

        // Send notification (email, Slack, etc.)
        await _notificationService.SendEscalationAsync(new EscalationNotification
        {
            AgentRole = agentRole,
            FailureCount = state.RetryCount,
            LastError = state.LastError,
            Checkpoint = await _checkpointService.GetLatestCheckpointAsync(agentRole),
            Artifacts = state.Artifacts
        });
    }
}
```

### Heartbeat Monitoring

```csharp
public class HeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeats = new();
    private readonly TimeoutPolicy _policy;

    public void RecordHeartbeat(string agentRole)
    {
        _lastHeartbeats[agentRole] = DateTime.UtcNow;
    }

    public bool IsAgentHealthy(string agentRole)
    {
        if (!_lastHeartbeats.TryGetValue(agentRole, out var lastHeartbeat))
            return false;

        var silentDuration = DateTime.UtcNow - lastHeartbeat;
        return silentDuration < _policy.HeartbeatTimeout;
    }

    public IReadOnlyList<string> GetUnhealthyAgents()
    {
        return _lastHeartbeats
            .Where(kvp => !IsAgentHealthy(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }
}
```

---

## Context Limit Management

### Checkpoint Model

```csharp
public record Checkpoint
{
    public required string AgentRole { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required string Summary { get; init; }
    public required CheckpointProgress Progress { get; init; }
    public required IReadOnlyList<string> CompletedItems { get; init; }
    public required IReadOnlyList<string> PendingItems { get; init; }
    public required IReadOnlyList<string> ActiveFiles { get; init; }
    public string? Notes { get; init; }
    public int? EstimatedContextUsage { get; init; }
}

public record CheckpointProgress
{
    public int CompletedTasks { get; init; }
    public int TotalTasks { get; init; }
    public double PercentComplete => TotalTasks > 0
        ? (double)CompletedTasks / TotalTasks * 100
        : 0;
}
```

### Checkpoint Service

```csharp
public class ContextCheckpointService : IContextCheckpointService
{
    private readonly IStateStore _store;
    private readonly ILogger<ContextCheckpointService> _logger;

    public async Task SaveCheckpointAsync(string agentRole, Checkpoint checkpoint)
    {
        await _store.SaveCheckpointAsync(agentRole, checkpoint);

        _logger.LogInformation(
            "Checkpoint saved for {Role}: {Progress}% complete, {Completed}/{Total} tasks",
            agentRole,
            checkpoint.Progress.PercentComplete,
            checkpoint.Progress.CompletedTasks,
            checkpoint.Progress.TotalTasks);
    }

    public async Task<Checkpoint?> GetLatestCheckpointAsync(string agentRole)
    {
        return await _store.GetLatestCheckpointAsync(agentRole);
    }

    public async Task<string> GenerateResumptionContextAsync(string agentRole)
    {
        var checkpoint = await GetLatestCheckpointAsync(agentRole);
        if (checkpoint == null)
            return "No previous checkpoint found. Start fresh.";

        return $"""
            ## Previous Session Checkpoint

            **Last Updated:** {checkpoint.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC

            ### Summary
            {checkpoint.Summary}

            ### Progress: {checkpoint.Progress.PercentComplete:F0}%

            #### Completed:
            {string.Join("\n", checkpoint.CompletedItems.Select(i => $"- [x] {i}"))}

            #### Remaining:
            {string.Join("\n", checkpoint.PendingItems.Select(i => $"- [ ] {i}"))}

            ### Active Files
            {string.Join("\n", checkpoint.ActiveFiles.Select(f => $"- {f}"))}

            ### Notes
            {checkpoint.Notes ?? "None"}

            ---
            **Continue from this checkpoint. Do not repeat completed work.**
            """;
    }
}
```

### Context-Aware Task Decomposition

```csharp
public class TaskDecomposer
{
    private const int SafeContextTokens = 50_000; // Conservative estimate
    private const int TokensPerFile = 15_000;    // Rough estimate per file

    public IReadOnlyList<WorkItem> DecomposeTask(WorkItem task)
    {
        var estimatedTokens = EstimateTaskTokens(task);

        if (estimatedTokens <= SafeContextTokens)
        {
            return new[] { task };
        }

        // Split into smaller work items
        var subtasks = new List<WorkItem>();
        var currentBatch = new List<string>();
        var currentEstimate = 0;

        foreach (var file in task.Files)
        {
            if (currentEstimate + TokensPerFile > SafeContextTokens && currentBatch.Any())
            {
                subtasks.Add(CreateSubtask(task, currentBatch, subtasks.Count + 1));
                currentBatch = new List<string>();
                currentEstimate = 0;
            }

            currentBatch.Add(file);
            currentEstimate += TokensPerFile;
        }

        if (currentBatch.Any())
        {
            subtasks.Add(CreateSubtask(task, currentBatch, subtasks.Count + 1));
        }

        return subtasks;
    }

    private int EstimateTaskTokens(WorkItem task)
    {
        return task.Files.Count * TokensPerFile;
    }

    private WorkItem CreateSubtask(WorkItem parent, List<string> files, int index)
    {
        return parent with
        {
            Id = $"{parent.Id}-{index}",
            ParentId = parent.Id,
            Files = files,
            Description = $"{parent.Description} (Part {index})"
        };
    }
}
```

---

## Monitoring & Observability

### Structured Logging with Serilog

```csharp
public static class LoggingConfiguration
{
    public static IHostBuilder ConfigureApmasLogging(this IHostBuilder builder)
    {
        return builder.UseSerilog((context, config) =>
        {
            config
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "APMAS")
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{AgentRole}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.Seq("http://localhost:5341")
                .WriteTo.File(
                    path: ".apmas/logs/apmas-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7);
        });
    }
}
```

### Metrics Collection

```csharp
public class ApmasMetrics
{
    private readonly Counter<int> _agentsSpawned;
    private readonly Counter<int> _agentsCompleted;
    private readonly Counter<int> _agentsFailed;
    private readonly Counter<int> _agentsTimedOut;
    private readonly Histogram<double> _agentDuration;
    private readonly Gauge<int> _activeAgents;

    public ApmasMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Apmas");

        _agentsSpawned = meter.CreateCounter<int>(
            "apmas.agents.spawned",
            description: "Number of agents spawned");

        _agentsCompleted = meter.CreateCounter<int>(
            "apmas.agents.completed",
            description: "Number of agents that completed successfully");

        _agentsFailed = meter.CreateCounter<int>(
            "apmas.agents.failed",
            description: "Number of agents that failed");

        _agentsTimedOut = meter.CreateCounter<int>(
            "apmas.agents.timedout",
            description: "Number of agents that timed out");

        _agentDuration = meter.CreateHistogram<double>(
            "apmas.agents.duration",
            unit: "seconds",
            description: "Agent execution duration");

        _activeAgents = meter.CreateGauge<int>(
            "apmas.agents.active",
            description: "Currently active agents");
    }

    public void RecordAgentSpawned(string role) =>
        _agentsSpawned.Add(1, new KeyValuePair<string, object?>("role", role));

    public void RecordAgentCompleted(string role, TimeSpan duration)
    {
        _agentsCompleted.Add(1, new KeyValuePair<string, object?>("role", role));
        _agentDuration.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("role", role));
    }

    public void RecordAgentFailed(string role, string reason)
    {
        _agentsFailed.Add(1,
            new KeyValuePair<string, object?>("role", role),
            new KeyValuePair<string, object?>("reason", reason));
    }

    public void RecordAgentTimedOut(string role) =>
        _agentsTimedOut.Add(1, new KeyValuePair<string, object?>("role", role));

    public void SetActiveAgents(int count) => _activeAgents.Record(count);
}
```

### Health Check Endpoint

```csharp
public class ApmasHealthCheck : IHealthCheck
{
    private readonly IAgentStateManager _stateManager;
    private readonly HeartbeatMonitor _heartbeatMonitor;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var projectState = await _stateManager.GetProjectStateAsync();
        var unhealthyAgents = _heartbeatMonitor.GetUnhealthyAgents();
        var activeAgents = await _stateManager.GetActiveAgentsAsync();

        var data = new Dictionary<string, object>
        {
            ["projectPhase"] = projectState.Phase.ToString(),
            ["activeAgents"] = activeAgents.Count,
            ["unhealthyAgents"] = unhealthyAgents.Count
        };

        if (unhealthyAgents.Any())
        {
            return HealthCheckResult.Degraded(
                $"Unhealthy agents: {string.Join(", ", unhealthyAgents)}",
                data: data);
        }

        if (projectState.Phase == ProjectPhase.Failed)
        {
            return HealthCheckResult.Unhealthy("Project in failed state", data: data);
        }

        return HealthCheckResult.Healthy("APMAS is running normally", data: data);
    }
}
```

---

## Data Models

### Project State

```csharp
public record ProjectState
{
    public required string Name { get; init; }
    public required string WorkingDirectory { get; init; }
    public required ProjectPhase Phase { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyDictionary<string, AgentState> Agents { get; init; } =
        new Dictionary<string, AgentState>();
}

public enum ProjectPhase
{
    Initializing,
    Planning,
    Building,
    Testing,
    Reviewing,
    Completing,
    Completed,
    Failed,
    Paused
}
```

### Agent State

```csharp
public record AgentState
{
    public required string Role { get; init; }
    public required AgentStatus Status { get; init; }
    public required string SubagentType { get; init; }
    public DateTime? SpawnedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? TimeoutAt { get; init; }
    public string? TaskId { get; init; }
    public int RetryCount { get; init; }
    public IReadOnlyList<string> Artifacts { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public string? LastMessage { get; init; }
    public string? LastError { get; init; }
    public int? EstimatedContextUsage { get; init; }
}
```

### Agent Message

```csharp
public record AgentMessage
{
    public required string Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required MessageType Type { get; init; }
    public required string Content { get; init; }
    public IReadOnlyList<string>? Artifacts { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

public enum MessageType
{
    Assignment,
    Progress,
    Question,
    Answer,
    Heartbeat,
    Checkpoint,
    Done,
    NeedsReview,
    Approved,
    ChangesRequested,
    Blocked,
    ContextLimit,
    Error
}
```

---

## MCP Tools Specification

### Tool: apmas_heartbeat

Agents call this periodically to signal they're alive.

```csharp
public class HeartbeatTool : IMcpTool
{
    public string Name => "apmas_heartbeat";
    public string Description => "Signal that you are still working. Call every 5 minutes.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["status"] = new() { Type = "string", Enum = new[] { "working", "thinking", "writing" } },
            ["progress"] = new() { Type = "string", Description = "Brief description of current work" },
            ["estimatedContextUsage"] = new() { Type = "integer", Description = "Estimated tokens used (if known)" }
        },
        Required = new[] { "status" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var status = input.GetProperty("status").GetString()!;
        var progress = input.TryGetProperty("progress", out var p) ? p.GetString() : null;
        var contextUsage = input.TryGetProperty("estimatedContextUsage", out var c) ? c.GetInt32() : (int?)null;

        _heartbeatMonitor.RecordHeartbeat(_currentAgent, status, progress, contextUsage);

        return ToolResult.Success("Heartbeat recorded");
    }
}
```

### Tool: apmas_report_status

Report task status and artifacts.

```csharp
public class ReportStatusTool : IMcpTool
{
    public string Name => "apmas_report_status";
    public string Description => "Report your current status and any artifacts created.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["status"] = new()
            {
                Type = "string",
                Enum = new[] { "working", "done", "blocked", "needs_review", "context_limit" }
            },
            ["message"] = new() { Type = "string", Description = "Status message" },
            ["artifacts"] = new()
            {
                Type = "array",
                Items = new() { Type = "string" },
                Description = "List of files created or modified"
            },
            ["blockedReason"] = new() { Type = "string", Description = "If blocked, explain why" }
        },
        Required = new[] { "status", "message" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var status = input.GetProperty("status").GetString()!;
        var message = input.GetProperty("message").GetString()!;
        var artifacts = input.TryGetProperty("artifacts", out var a)
            ? a.EnumerateArray().Select(x => x.GetString()!).ToList()
            : new List<string>();

        await _messageBus.PublishAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            From = _currentAgent,
            To = "supervisor",
            Type = MapStatusToMessageType(status),
            Content = message,
            Artifacts = artifacts
        });

        await _stateManager.UpdateAgentStateAsync(_currentAgent, state => state with
        {
            Status = MapStatusToAgentStatus(status),
            LastMessage = message,
            Artifacts = state.Artifacts.Concat(artifacts).Distinct().ToList()
        });

        return ToolResult.Success($"Status '{status}' recorded");
    }
}
```

### Tool: apmas_checkpoint

Save progress checkpoint for context recovery.

```csharp
public class CheckpointTool : IMcpTool
{
    public string Name => "apmas_checkpoint";
    public string Description => "Save a checkpoint of your progress. Call when completing subtasks or if approaching context limits.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["summary"] = new() { Type = "string", Description = "Brief summary of current state" },
            ["completedItems"] = new() { Type = "array", Items = new() { Type = "string" } },
            ["pendingItems"] = new() { Type = "array", Items = new() { Type = "string" } },
            ["activeFiles"] = new() { Type = "array", Items = new() { Type = "string" } },
            ["notes"] = new() { Type = "string", Description = "Any notes for continuation" }
        },
        Required = new[] { "summary", "completedItems", "pendingItems" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var checkpoint = new Checkpoint
        {
            AgentRole = _currentAgent,
            CreatedAt = DateTime.UtcNow,
            Summary = input.GetProperty("summary").GetString()!,
            CompletedItems = input.GetProperty("completedItems").EnumerateArray()
                .Select(x => x.GetString()!).ToList(),
            PendingItems = input.GetProperty("pendingItems").EnumerateArray()
                .Select(x => x.GetString()!).ToList(),
            ActiveFiles = input.TryGetProperty("activeFiles", out var af)
                ? af.EnumerateArray().Select(x => x.GetString()!).ToList()
                : Array.Empty<string>(),
            Notes = input.TryGetProperty("notes", out var n) ? n.GetString() : null,
            Progress = new CheckpointProgress
            {
                CompletedTasks = input.GetProperty("completedItems").GetArrayLength(),
                TotalTasks = input.GetProperty("completedItems").GetArrayLength() +
                            input.GetProperty("pendingItems").GetArrayLength()
            }
        };

        await _checkpointService.SaveCheckpointAsync(_currentAgent, checkpoint);

        return ToolResult.Success($"Checkpoint saved: {checkpoint.Progress.PercentComplete:F0}% complete");
    }
}
```

### Tool: apmas_get_context

Get project context and other agents' outputs.

```csharp
public class GetContextTool : IMcpTool
{
    public string Name => "apmas_get_context";
    public string Description => "Get current project context, other agents' outputs, and messages.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["include"] = new()
            {
                Type = "array",
                Items = new() { Type = "string" },
                Description = "What to include: 'project', 'agents', 'messages', 'artifacts'"
            },
            ["agentRoles"] = new()
            {
                Type = "array",
                Items = new() { Type = "string" },
                Description = "Specific agent roles to get info about"
            },
            ["messageLimit"] = new() { Type = "integer", Description = "Max messages to return" }
        }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var include = input.TryGetProperty("include", out var i)
            ? i.EnumerateArray().Select(x => x.GetString()!).ToHashSet()
            : new HashSet<string> { "project", "agents", "messages" };

        var result = new Dictionary<string, object>();

        if (include.Contains("project"))
        {
            result["project"] = await _stateManager.GetProjectStateAsync();
        }

        if (include.Contains("agents"))
        {
            var roles = input.TryGetProperty("agentRoles", out var ar)
                ? ar.EnumerateArray().Select(x => x.GetString()!).ToList()
                : null;

            result["agents"] = roles != null
                ? await Task.WhenAll(roles.Select(r => _stateManager.GetAgentStateAsync(r)))
                : await _stateManager.GetActiveAgentsAsync();
        }

        if (include.Contains("messages"))
        {
            var limit = input.TryGetProperty("messageLimit", out var ml) ? ml.GetInt32() : 50;
            result["messages"] = await _messageBus.GetAllMessagesAsync(limit);
        }

        return ToolResult.Success(JsonSerializer.Serialize(result));
    }
}
```

### Tool: apmas_send_message

Send a message to another agent or broadcast.

```csharp
public class SendMessageTool : IMcpTool
{
    public string Name => "apmas_send_message";
    public string Description => "Send a message to another agent or broadcast to all.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["to"] = new() { Type = "string", Description = "Target agent role or 'all' for broadcast" },
            ["type"] = new()
            {
                Type = "string",
                Enum = new[] { "question", "answer", "info", "request" }
            },
            ["content"] = new() { Type = "string", Description = "Message content" }
        },
        Required = new[] { "to", "type", "content" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var message = new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            From = _currentAgent,
            To = input.GetProperty("to").GetString()!,
            Type = Enum.Parse<MessageType>(input.GetProperty("type").GetString()!, ignoreCase: true),
            Content = input.GetProperty("content").GetString()!
        };

        await _messageBus.PublishAsync(message);

        return ToolResult.Success($"Message sent to {message.To}");
    }
}
```

### Tool: apmas_request_help

Request human intervention or another agent's assistance.

```csharp
public class RequestHelpTool : IMcpTool
{
    public string Name => "apmas_request_help";
    public string Description => "Request help when blocked. Can request human intervention or another agent.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["helpType"] = new()
            {
                Type = "string",
                Enum = new[] { "human", "agent", "clarification" }
            },
            ["targetAgent"] = new() { Type = "string", Description = "If requesting agent help, which agent" },
            ["issue"] = new() { Type = "string", Description = "What you need help with" },
            ["context"] = new() { Type = "string", Description = "Relevant context" }
        },
        Required = new[] { "helpType", "issue" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var helpType = input.GetProperty("helpType").GetString()!;
        var issue = input.GetProperty("issue").GetString()!;

        if (helpType == "human")
        {
            await _stateManager.UpdateAgentStateAsync(_currentAgent, s => s with
            {
                Status = AgentStatus.Escalated,
                LastError = issue
            });

            await _notificationService.SendEscalationAsync(new EscalationNotification
            {
                AgentRole = _currentAgent,
                Issue = issue,
                Context = input.TryGetProperty("context", out var c) ? c.GetString() : null
            });

            return ToolResult.Success("Human intervention requested. Pausing work.");
        }

        // Handle agent-to-agent help request
        var targetAgent = input.GetProperty("targetAgent").GetString()!;

        await _messageBus.PublishAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            From = _currentAgent,
            To = targetAgent,
            Type = MessageType.Question,
            Content = issue
        });

        return ToolResult.Success($"Help request sent to {targetAgent}");
    }
}
```

### Tool: apmas_complete

Signal task completion.

```csharp
public class CompleteTool : IMcpTool
{
    public string Name => "apmas_complete";
    public string Description => "Signal that your task is complete. Call when all work is done.";

    public JsonSchema InputSchema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, JsonSchema>
        {
            ["summary"] = new() { Type = "string", Description = "Summary of what was accomplished" },
            ["artifacts"] = new()
            {
                Type = "array",
                Items = new() { Type = "string" },
                Description = "List of all files created or modified"
            },
            ["notes"] = new() { Type = "string", Description = "Any notes for downstream agents" }
        },
        Required = new[] { "summary", "artifacts" }
    };

    public async Task<ToolResult> ExecuteAsync(JsonElement input)
    {
        var summary = input.GetProperty("summary").GetString()!;
        var artifacts = input.GetProperty("artifacts").EnumerateArray()
            .Select(x => x.GetString()!).ToList();

        await _stateManager.UpdateAgentStateAsync(_currentAgent, s => s with
        {
            Status = AgentStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            Artifacts = artifacts,
            LastMessage = summary
        });

        await _messageBus.PublishAsync(new AgentMessage
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            From = _currentAgent,
            To = "supervisor",
            Type = MessageType.Done,
            Content = summary,
            Artifacts = artifacts
        });

        _metrics.RecordAgentCompleted(_currentAgent,
            DateTime.UtcNow - (await _stateManager.GetAgentStateAsync(_currentAgent)).SpawnedAt!.Value);

        return ToolResult.Success("Task marked complete. You may stop working.");
    }
}
```

---

## Agent Prompts

### Base Agent Prompt Template

```csharp
public abstract class BaseAgentPrompt
{
    public abstract string Role { get; }
    public abstract string SubagentType { get; }

    public string Generate(ProjectState project, string? additionalContext = null)
    {
        return $"""
            # {Role} Agent - APMAS Project

            You are the **{Role}** for the "{project.Name}" project.

            ## Your Role
            {GetRoleDescription()}

            ## Working Directory
            {project.WorkingDirectory}

            ## APMAS Communication

            You have access to APMAS MCP tools for coordination. **USE THEM.**

            ### Required Tool Usage:

            1. **Heartbeat (every 5 minutes)**
               Call `apmas_heartbeat` while working to signal you're alive.
               ```
               apmas_heartbeat(status: "working", progress: "building index.html")
               ```

            2. **Checkpoint (after each subtask)**
               Call `apmas_checkpoint` to save progress for recovery.
               ```
               apmas_checkpoint(
                 summary: "Completed homepage layout",
                 completedItems: ["header", "hero section"],
                 pendingItems: ["footer", "post grid"]
               )
               ```

            3. **Status Updates**
               Call `apmas_report_status` for significant updates.
               ```
               apmas_report_status(status: "done", message: "Architecture complete", artifacts: ["docs/architecture.md"])
               ```

            4. **Completion**
               Call `apmas_complete` when ALL work is done.
               ```
               apmas_complete(summary: "Built all pages", artifacts: ["src/index.html", "src/post.html"])
               ```

            ### Context Management

            If you feel your responses getting shorter or you're losing context:
            1. Immediately call `apmas_checkpoint` with your current progress
            2. Call `apmas_report_status(status: "context_limit", message: "Approaching context limits")`
            3. Stop work - the Supervisor will respawn you with your checkpoint

            ### Getting Help

            If blocked, use `apmas_request_help`:
            ```
            apmas_request_help(helpType: "clarification", issue: "Design spec unclear on button colors")
            ```

            ## Your Task
            {GetTaskDescription()}

            ## Deliverables
            {GetDeliverables()}

            ## Dependencies
            {GetDependencies()}

            {(additionalContext != null ? $"## Additional Context\n{additionalContext}" : "")}

            ---

            **BEGIN:** Start your work now. Remember to call `apmas_heartbeat` every 5 minutes.
            """;
    }

    protected abstract string GetRoleDescription();
    protected abstract string GetTaskDescription();
    protected abstract string GetDeliverables();
    protected abstract string GetDependencies();
}
```

### Example: Developer Agent Prompt

```csharp
public class DeveloperPrompt : BaseAgentPrompt
{
    public override string Role => "Developer";
    public override string SubagentType => "html-prototyper"; // or "dotnet-specialist"

    protected override string GetRoleDescription() => """
        You implement features based on architecture and design specifications.
        You write clean, maintainable code following best practices.
        You create files, write tests, and ensure quality.
        """;

    protected override string GetTaskDescription() => """
        Implement the required features according to the architecture and design specs.
        Read the specs first, then build each component methodically.
        """;

    protected override string GetDeliverables() => """
        - Source code files in src/
        - Unit tests (if applicable)
        - Updated documentation (if needed)
        """;

    protected override string GetDependencies() => """
        Wait for these before starting:
        - docs/architecture.md (from Architect)
        - docs/design-spec.md (from Designer)

        Use `apmas_get_context` to check if these exist.
        """;
}
```

---

## Implementation Plan

### Phase 1: Core Infrastructure (Week 1)

1. **Project Setup**
   - Create .NET 8 solution with projects
   - Configure DI, logging, configuration
   - Set up SQLite for state persistence

2. **Data Models**
   - Implement all record types
   - Create EF Core DbContext
   - Add migrations

3. **State Management**
   - Implement `AgentStateManager`
   - Implement `MessageBus`
   - Add file watching for external changes

### Phase 2: MCP Server (Week 2)

1. **MCP Protocol Implementation**
   - Implement MCP server base
   - Add stdio transport
   - Handle tool calls

2. **Tool Handlers**
   - Implement all 7 tools
   - Add validation
   - Add error handling

3. **Resources**
   - Implement resource handlers
   - Add caching

### Phase 3: Supervisor Service (Week 3)

1. **Core Supervisor**
   - Implement background service
   - Add polling loop
   - Add dependency resolution

2. **Agent Spawner**
   - Implement Claude Code CLI integration
   - Add process management
   - Handle stdout/stderr

3. **Timeout Handling**
   - Implement timeout detection
   - Add retry logic
   - Add escalation

### Phase 4: Agent Integration (Week 4)

1. **Agent Prompts**
   - Create prompt templates
   - Test with real agents
   - Iterate on instructions

2. **End-to-End Testing**
   - Test full workflow
   - Test failure scenarios
   - Test recovery

3. **Documentation**
   - API documentation
   - Usage guide
   - Troubleshooting guide

---

## Configuration File

```json
{
  "Apmas": {
    "ProjectName": "my-project",
    "WorkingDirectory": "C:/projects/my-project",
    "DataDirectory": ".apmas",
    "Timeouts": {
      "DefaultMinutes": 30,
      "HeartbeatIntervalMinutes": 5,
      "HeartbeatTimeoutMinutes": 10,
      "MaxRetries": 3,
      "AgentOverrides": {
        "architect": 15,
        "developer": 45,
        "reviewer": 20
      }
    },
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": []
        },
        {
          "Role": "designer",
          "SubagentType": "design-specialist",
          "Dependencies": []
        },
        {
          "Role": "developer",
          "SubagentType": "html-prototyper",
          "Dependencies": ["architect", "designer"]
        },
        {
          "Role": "reviewer",
          "SubagentType": "code-reviewer",
          "Dependencies": ["developer"]
        }
      ]
    },
    "Notifications": {
      "EscalationEmail": "team@example.com",
      "SlackWebhook": "https://hooks.slack.com/..."
    },
    "Observability": {
      "SeqUrl": "http://localhost:5341",
      "EnableMetrics": true
    }
  }
}
```

---

## File Structure Summary

```
Apmas.Server/
├── Apmas.Server.csproj
├── Program.cs
├── appsettings.json
├── Configuration/
├── Core/
│   ├── Services/
│   ├── Models/
│   └── Enums/
├── Mcp/
│   ├── ApmasMcpServer.cs
│   ├── Tools/
│   └── Resources/
├── Agents/
│   ├── Prompts/
│   └── Definitions/
└── Storage/

Project Directory Structure:
project/
├── .apmas/
│   ├── state.db           # SQLite database
│   ├── logs/              # Log files
│   └── checkpoints/       # Agent checkpoints
├── docs/
├── src/
└── tests/
```

---

## Success Metrics

| Metric | Target |
|--------|--------|
| Agent success rate | > 95% |
| Average timeout recovery | < 2 retries |
| Human escalations | < 5% of tasks |
| Context limit recoveries | 100% successful |
| End-to-end project completion | > 90% |

---

## Appendix: MCP Protocol Reference

The MCP server implements the Model Context Protocol specification:

- **Transport**: stdio (for Claude Code integration)
- **Capabilities**: tools, resources
- **Tools**: 7 custom tools for agent coordination
- **Resources**: Project state, messages, checkpoints

For MCP protocol details, see: https://modelcontextprotocol.io/

---

*Document Version: 1.0*
*Last Updated: 2026-01-20*