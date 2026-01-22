# APMAS: Autonomous Project Management Agent System

## .NET 8 MCP Server Specification

A comprehensive specification for a multi-agent coordination system using a .NET MCP (Model Context Protocol) server as the central orchestration layer.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Overview](#architecture-overview)
3. [Core Services](#core-services)
4. [MCP Server Design](#mcp-server-design)
5. [MCP Tools Specification](#mcp-tools-specification)
6. [MCP Resources](#mcp-resources)
7. [Agent Lifecycle Management](#agent-lifecycle-management)
8. [Timeout Handling](#timeout-handling)
9. [Context Limit Management](#context-limit-management)
10. [Data Models](#data-models)
11. [Configuration Reference](#configuration-reference)
12. [Transport Mechanisms](#transport-mechanisms)
13. [Storage Layer](#storage-layer)
14. [Monitoring & Observability](#monitoring--observability)
15. [Agent Prompts](#agent-prompts)

---

## Executive Summary

APMAS is a multi-agent coordination system that enables autonomous AI agents to collaborate on software projects. The system addresses three critical challenges:

1. **Agent Timeouts**: Agents that hang or take too long
2. **Context Limits**: Agents exhausting their context window
3. **Monitoring & Restart**: Detecting failures and recovering gracefully

The key architectural insight: **Agents cannot self-coordinate reliably**. An external orchestrator (the MCP server) must manage agent lifecycle, dependencies, and communication.

### Key Features

- **HTTP/SSE Transport**: Spawned agents connect back via HTTP with Server-Sent Events
- **SQLite Persistence**: All state stored in a local SQLite database
- **Background Supervisor**: Continuous monitoring and lifecycle management
- **Progressive Retry**: Checkpoint recovery, scope reduction, then human escalation
- **Inter-Agent Messaging**: Guaranteed delivery message bus

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
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │  Heartbeat  │  │  Timeout    │  │ Checkpoint  │  │ Dependency  │        │
│  │  Monitor    │  │  Handler    │  │  Service    │  │  Resolver   │        │
│  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘        │
│                                    │                                        │
│  ┌─────────────────────────────────┴─────────────────────────────────┐     │
│  │                    MCP Tool Handlers                               │     │
│  │  • apmas_heartbeat    • apmas_report_status   • apmas_get_context │     │
│  │  • apmas_checkpoint   • apmas_request_help    • apmas_complete    │     │
│  │  • apmas_send_message                                              │     │
│  └───────────────────────────────────────────────────────────────────┘     │
│                                    │                                        │
│  ┌─────────────────────────────────┴─────────────────────────────────┐     │
│  │              HTTP/SSE MCP Transport (HttpMcpServerHost)            │     │
│  │                    localhost:5050 (configurable)                   │     │
│  └───────────────────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │ HTTP/SSE            │ HTTP/SSE            │ HTTP/SSE
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
    │  ├── .apmas/           (state.db, logs/, checkpoints/)      │
    │  ├── docs/             (documentation artifacts)            │
    │  ├── src/              (source code artifacts)              │
    │  └── tests/            (test artifacts)                     │
    └─────────────────────────────────────────────────────────────┘
```

### Runtime Data Structure

```
.apmas/                    # Data directory (configurable)
├── state.db               # SQLite database with all state
├── logs/                  # Serilog rolling daily logs
│   ├── apmas-20260122.log
│   └── apmas-20260121.log
└── checkpoints/           # Agent progress checkpoints (future)
```

---

## Core Services

| Service | Interface | Responsibility |
|---------|-----------|----------------|
| **SupervisorService** | BackgroundService | Polls periodically to monitor agent health, check dependencies, and spawn ready agents |
| **AgentStateManager** | IAgentStateManager | Single source of truth for project and agent state; manages state caching and transitions |
| **MessageBus** | IMessageBus | Inter-agent messaging with guaranteed persistence and real-time subscription support |
| **ClaudeCodeSpawner** | IAgentSpawner | Spawns Claude Code agents via CLI and manages their process lifecycle |
| **HeartbeatMonitor** | IHeartbeatMonitor | Tracks agent liveliness; detects unhealthy agents based on heartbeat timeout |
| **TimeoutHandler** | ITimeoutHandler | Implements progressive retry strategy on agent timeout |
| **ContextCheckpointService** | IContextCheckpointService | Manages agent checkpoints and generates resumption context |
| **DependencyResolver** | IDependencyResolver | Analyzes agent dependency graph and validates configuration at startup |
| **TaskDecomposerService** | ITaskDecomposerService | Decomposes work items into subtasks based on context size limits |
| **NotificationService** | INotificationService | Sends escalation notifications (Console, Email, or Slack) |
| **ApmasMetrics** | IApmasMetrics | Collects observability metrics (agent counts, task durations, timeouts) |
| **ApmasHealthCheck** | IHealthCheck | Reports system health status |

### Service Interfaces

```csharp
public interface IAgentStateManager
{
    Task<ProjectState?> GetProjectStateAsync();
    Task<AgentState?> GetAgentStateAsync(string role);
    Task<IReadOnlyList<AgentState>> GetAllAgentStatesAsync();
    Task SaveProjectStateAsync(ProjectState state);
    Task SaveAgentStateAsync(AgentState state);
}

public interface IMessageBus
{
    Task PublishAsync(AgentMessage message);
    Task<IReadOnlyList<AgentMessage>> GetMessagesForAgentAsync(string agentRole, DateTime? since = null);
    Task<IReadOnlyList<AgentMessage>> GetAllMessagesAsync(int? limit = null);
    IAsyncEnumerable<AgentMessage> SubscribeAsync(string? agentRole = null, CancellationToken ct = default);
}

public interface IAgentSpawner
{
    Task<SpawnResult> SpawnAgentAsync(string role, string subagentType, string? checkpointContext = null);
    Task TerminateAgentAsync(string agentRole);
    Task<AgentProcessInfo?> GetAgentProcessAsync(string agentRole);
    Task ShutdownAllAgentsAsync();
}

public interface IHeartbeatMonitor
{
    void RecordHeartbeat(string agentRole);
    bool IsAgentHealthy(string agentRole);
    Task<IReadOnlyList<string>> GetUnhealthyAgentsAsync();
}

public interface ITimeoutHandler
{
    Task HandleTimeoutAsync(string agentRole);
}

public interface IContextCheckpointService
{
    Task SaveCheckpointAsync(Checkpoint checkpoint);
    Task<Checkpoint?> GetLatestCheckpointAsync(string agentRole);
    Task<string> GenerateResumptionContextAsync(string agentRole);
}

public interface IDependencyResolver
{
    Task<IReadOnlyList<string>> GetReadyAgentsAsync();
    (bool IsValid, IReadOnlyList<string> Errors) ValidateDependencyGraph();
}

public interface INotificationService
{
    Task SendEscalationAsync(EscalationNotification notification);
}
```

---

## MCP Server Design

### Project Structure

```
src/Apmas.Server/
├── Configuration/              # Options pattern configuration classes
│   ├── ApmasOptions.cs
│   ├── AgentOptions.cs
│   ├── TimeoutOptions.cs
│   ├── SpawnerOptions.cs
│   ├── HttpTransportOptions.cs
│   ├── DecompositionOptions.cs
│   ├── NotificationOptions.cs
│   └── MetricsOptions.cs
│
├── Core/
│   ├── Services/               # Core business logic
│   │   ├── SupervisorService.cs
│   │   ├── AgentStateManager.cs
│   │   ├── MessageBus.cs
│   │   ├── HeartbeatMonitor.cs
│   │   ├── TimeoutHandler.cs
│   │   ├── ContextCheckpointService.cs
│   │   ├── DependencyResolver.cs
│   │   ├── TaskDecomposerService.cs
│   │   ├── ConsoleNotificationService.cs
│   │   ├── EmailNotificationService.cs
│   │   ├── SlackNotificationService.cs
│   │   ├── ApmasMetrics.cs
│   │   └── ApmasHealthCheck.cs
│   │
│   ├── Models/                 # Data models (EF Core entities)
│   │   ├── ProjectState.cs
│   │   ├── AgentState.cs
│   │   ├── AgentMessage.cs
│   │   ├── Checkpoint.cs
│   │   ├── WorkItem.cs
│   │   ├── EscalationNotification.cs
│   │   ├── AgentProcessInfo.cs
│   │   └── SpawnResult.cs
│   │
│   └── Enums/
│       ├── AgentStatus.cs
│       ├── MessageType.cs
│       └── ProjectPhase.cs
│
├── Mcp/
│   ├── IMcpTool.cs
│   ├── ToolResult.cs
│   ├── IMcpResource.cs
│   ├── ResourceResult.cs
│   ├── McpToolRegistry.cs
│   ├── McpResourceRegistry.cs
│   ├── McpServerHost.cs
│   │
│   ├── Tools/
│   │   ├── HeartbeatTool.cs
│   │   ├── CheckpointTool.cs
│   │   ├── GetContextTool.cs
│   │   ├── ReportStatusTool.cs
│   │   ├── SendMessageTool.cs
│   │   ├── CompleteTool.cs
│   │   └── RequestHelpTool.cs
│   │
│   ├── Resources/
│   │   ├── ProjectStateResource.cs
│   │   ├── AgentMessagesResource.cs
│   │   └── CheckpointResource.cs
│   │
│   └── Http/                   # HTTP/SSE transport
│       ├── HttpMcpServerHost.cs
│       └── IHttpServerReadySignal.cs
│
├── Agents/
│   ├── Prompts/
│   │   ├── IPromptFactory.cs
│   │   ├── PromptFactory.cs
│   │   ├── BaseAgentPrompt.cs
│   │   ├── ArchitectPrompt.cs
│   │   ├── DesignerPrompt.cs
│   │   ├── DeveloperPrompt.cs
│   │   ├── ReviewerPrompt.cs
│   │   └── TesterPrompt.cs
│   │
│   ├── Definitions/
│   │   └── AgentRoster.cs
│   │
│   ├── ClaudeCodeSpawner.cs
│   └── NativeMethods.cs
│
├── Storage/
│   ├── IStateStore.cs
│   ├── SqliteStateStore.cs
│   ├── ApmasDbContext.cs
│   └── Migrations/
│
├── Program.cs
└── appsettings.json
```

### Dependency Injection Setup

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Bootstrap logger (console + Seq only)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateBootstrapLogger();

builder.Services.AddSerilog((services, config) => config
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.WithProperty("Application", "APMAS"));

// Configuration
builder.Services.Configure<ApmasOptions>(
    builder.Configuration.GetSection("Apmas"));

// Storage layer
builder.Services.AddStorageServices();

// Core services
builder.Services.AddCoreServices();

// Agent roster and spawner
builder.Services.AddAgentRoster();
builder.Services.AddAgentSpawner();

// MCP server and tools
builder.Services.AddMcpServer();
builder.Services.AddMcpTools();
builder.Services.AddMcpResources();

// Background services
builder.Services.AddHostedService<HttpMcpServerHost>();
builder.Services.AddHostedService<SupervisorService>();

var host = builder.Build();

// Initialize database
await host.Services.GetRequiredService<IStateStore>().EnsureStorageCreatedAsync();

// Initialize project state from configuration
await InitializeFromConfigAsync(host.Services);

// Validate dependency graph
var resolver = host.Services.GetRequiredService<IDependencyResolver>();
var (isValid, errors) = resolver.ValidateDependencyGraph();
if (!isValid)
{
    Log.Fatal("Invalid dependency graph: {Errors}", errors);
    return 1;
}

await host.RunAsync();
```

---

## MCP Tools Specification

All tools require an `agentRole` parameter to identify the calling agent.

### apmas_heartbeat

Signal alive status and extend timeout window.

**When to call**: Every 5 minutes while working.

```json
{
  "name": "apmas_heartbeat",
  "description": "Signal that you are still working. Call every 5 minutes.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier"
      },
      "status": {
        "type": "string",
        "enum": ["working", "thinking", "writing"],
        "description": "Current activity status"
      },
      "progress": {
        "type": "string",
        "description": "Brief description of current work"
      },
      "estimatedContextUsage": {
        "type": "integer",
        "description": "Estimated tokens used (if known)"
      }
    },
    "required": ["agentRole", "status"]
  }
}
```

**Returns**: Success message with new timeout timestamp.

**Implementation**: Updates agent's `LastHeartbeat` and `TimeoutAt`, records heartbeat in monitor.

---

### apmas_checkpoint

Save progress checkpoint for recovery after timeout.

**When to call**: After completing major subtasks.

```json
{
  "name": "apmas_checkpoint",
  "description": "Save a checkpoint of your progress for recovery.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier"
      },
      "summary": {
        "type": "string",
        "description": "Brief summary of current state"
      },
      "completedItems": {
        "type": "array",
        "items": { "type": "string" },
        "description": "List of completed work items"
      },
      "pendingItems": {
        "type": "array",
        "items": { "type": "string" },
        "description": "List of remaining work items"
      },
      "activeFiles": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Files currently being worked on"
      },
      "notes": {
        "type": "string",
        "description": "Notes for continuation"
      }
    },
    "required": ["agentRole", "summary", "completedItems", "pendingItems"]
  }
}
```

**Returns**: Success message with completion percentage.

---

### apmas_get_context

Retrieve project state, agent outputs, and messages for coordination.

**When to call**: Before making decisions; to understand other agents' work; when blocked.

```json
{
  "name": "apmas_get_context",
  "description": "Get current project context, other agents' outputs, and messages.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "include": {
        "type": "array",
        "items": { "type": "string" },
        "description": "What to include: 'project', 'agents', 'messages', 'artifacts'"
      },
      "agentRoles": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Specific agent roles to get info about"
      },
      "messageLimit": {
        "type": "integer",
        "description": "Max messages to return (default: 50)"
      }
    }
  }
}
```

**Returns**: JSON object with requested sections:

```json
{
  "project": {
    "name": "my-project",
    "workingDirectory": "/path/to/project",
    "phase": "Building",
    "startedAt": "2026-01-22T10:00:00Z"
  },
  "agents": [
    {
      "role": "architect",
      "status": "Completed",
      "lastMessage": "Architecture complete",
      "spawnedAt": "2026-01-22T10:05:00Z"
    }
  ],
  "messages": [...],
  "artifacts": ["docs/architecture.md", "src/index.html"]
}
```

---

### apmas_report_status

Report current status, progress, and artifacts.

**When to call**: Regularly during work; when status changes.

```json
{
  "name": "apmas_report_status",
  "description": "Report your current status and any artifacts created.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier"
      },
      "status": {
        "type": "string",
        "enum": ["working", "done", "blocked", "needs_review", "context_limit"],
        "description": "Current status"
      },
      "message": {
        "type": "string",
        "description": "Status message"
      },
      "artifacts": {
        "type": "array",
        "items": { "type": "string" },
        "description": "List of files created or modified"
      },
      "blockedReason": {
        "type": "string",
        "description": "If blocked, explain why"
      }
    },
    "required": ["agentRole", "status", "message"]
  }
}
```

**Returns**: Confirmation message.

**Special handling**:
- `blocked`: Requires `blockedReason`
- `context_limit`: Sets agent to Paused status for resumption

---

### apmas_send_message

Send a message to another agent or broadcast.

**When to call**: To ask questions, request help, or share information.

```json
{
  "name": "apmas_send_message",
  "description": "Send a message to another agent or broadcast to all.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier (sender)"
      },
      "to": {
        "type": "string",
        "description": "Target agent role or 'all' for broadcast"
      },
      "type": {
        "type": "string",
        "enum": ["question", "answer", "info", "request"],
        "description": "Message type"
      },
      "content": {
        "type": "string",
        "description": "Message content"
      }
    },
    "required": ["agentRole", "to", "type", "content"]
  }
}
```

**Returns**: Confirmation with message ID.

---

### apmas_request_help

Request human intervention or another agent's assistance.

**When to call**: When stuck, encountering unknown issues, or needing decisions.

```json
{
  "name": "apmas_request_help",
  "description": "Request help when blocked.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier"
      },
      "helpType": {
        "type": "string",
        "enum": ["human", "agent", "clarification"],
        "description": "Type of help needed"
      },
      "targetAgent": {
        "type": "string",
        "description": "If requesting agent help, which agent"
      },
      "issue": {
        "type": "string",
        "description": "What you need help with"
      },
      "context": {
        "type": "string",
        "description": "Relevant context"
      }
    },
    "required": ["agentRole", "helpType", "issue"]
  }
}
```

**Behavior**:
- `human`: Sets status to Escalated; sends notification via configured provider
- `agent`: Sends Question message to target agent
- `clarification`: Sends Question message to supervisor

---

### apmas_complete

Signal task completion.

**When to call**: When all assigned work is finished.

```json
{
  "name": "apmas_complete",
  "description": "Signal that your task is complete.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": {
        "type": "string",
        "description": "Your agent role identifier"
      },
      "summary": {
        "type": "string",
        "description": "Summary of what was accomplished"
      },
      "artifacts": {
        "type": "array",
        "items": { "type": "string" },
        "description": "List of all files created or modified"
      },
      "notes": {
        "type": "string",
        "description": "Notes for downstream agents"
      }
    },
    "required": ["agentRole", "summary", "artifacts"]
  }
}
```

**Returns**: Success message with execution duration.

---

## MCP Resources

Resources provide read-only access to data via URI patterns.

### apmas://project/state

Current project state as JSON.

**URI**: `apmas://project/state`

**Returns**:
```json
{
  "name": "my-project",
  "workingDirectory": "/path/to/project",
  "phase": "Building",
  "startedAt": "2026-01-22T10:00:00Z",
  "completedAt": null
}
```

**Caching**: 5 seconds to avoid database thrashing.

---

### apmas://messages/{agentRole}

Messages addressed to or from a specific agent.

**URI**: `apmas://messages/{agentRole}`

**Returns**: Filtered message list including broadcasts (To="all").

---

### apmas://checkpoints/{agentRole}

Checkpoint history for an agent.

**URI**: `apmas://checkpoints/{agentRole}`

**Returns**: Array of checkpoints ordered by creation time (newest first).

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
┌─────────────────────────────────────────────────────────────┐
│                      Agent Lifecycle                         │
└─────────────────────────────────────────────────────────────┘

    ┌────────────┐
    │  Pending   │ (waiting for dependencies)
    └─────┬──────┘
          │ (dependencies met)
    ┌─────▼──────┐
    │  Queued    │ (ready to spawn)
    └─────┬──────┘
          │ (spawned)
    ┌─────▼─────────┐
    │  Spawning     │ (launching process)
    └─────┬─────────┘
          │
    ┌─────▼────────┐
    │  Running     │ (actively working)
    └─┬──────────┬─┘
      │          │
(checkpoint) (timeout)
      │          │
    ┌─▼────┐   ┌─▼────────────┐
    │Paused│   │  TimedOut    │
    └─┬────┘   └─┬────────────┘
      │          │ (retry strategy)
      │        ┌─▼─┐
      │        │ 1 │ restart with checkpoint
      │        │ 2 │ restart with reduced scope
      │        │ 3 │ escalate to human
      │        └───┘
      │          │
      └────┬─────┘
           │ (resume)
      ┌────▼──────┐
      │  Running  │
      └─┬─────────┘
        │
   (completes)
        │
    ┌───▼──────────┐
    │  Completed   │
    └──────────────┘

   Alternative:
    ┌────────┐
    │ Failed │ (internal error)
    └────────┘

    ┌──────────┐
    │Escalated │ (human intervention needed)
    └──────────┘
```

### Dependency Resolution

```csharp
public class DependencyResolver : IDependencyResolver
{
    public async Task<IReadOnlyList<string>> GetReadyAgentsAsync()
    {
        var allAgents = await _stateManager.GetAllAgentStatesAsync();
        var readyAgents = new List<string>();

        foreach (var agent in allAgents.Where(a => a.Status == AgentStatus.Pending))
        {
            var dependencies = GetDependencies(agent.Role);
            var allMet = dependencies.All(dep =>
                allAgents.Any(a => a.Role == dep && a.Status == AgentStatus.Completed));

            if (allMet)
            {
                readyAgents.Add(agent.Role);
            }
        }

        return readyAgents;
    }

    public (bool IsValid, IReadOnlyList<string> Errors) ValidateDependencyGraph()
    {
        var errors = new List<string>();

        // Check for undefined dependencies
        // Check for circular dependencies
        // Return validation result

        return (errors.Count == 0, errors);
    }
}
```

---

## Timeout Handling

### Timeout Strategy (Progressive Retry)

1. **First Timeout** (RetryCount = 0):
   - Generate resumption context from latest checkpoint
   - Restart agent with recovery context
   - Increment RetryCount to 1

2. **Second Timeout** (RetryCount = 1):
   - Restart with reduced scope (if supported)
   - Increment RetryCount to 2

3. **Third+ Timeout** (RetryCount >= 2):
   - Set status to Escalated
   - Create EscalationNotification
   - Send to notification service
   - Human manually intervenes

### TimeoutHandler Implementation

```csharp
public class TimeoutHandler : ITimeoutHandler
{
    public async Task HandleTimeoutAsync(string agentRole)
    {
        var state = await _stateManager.GetAgentStateAsync(agentRole);
        if (state == null) return;

        state.RetryCount++;

        _logger.LogWarning(
            "Agent {Role} timed out (attempt {Attempt}/{Max})",
            agentRole, state.RetryCount, _options.MaxRetries);

        if (state.RetryCount == 1)
        {
            await RestartWithCheckpointAsync(agentRole, state);
        }
        else if (state.RetryCount == 2)
        {
            await RestartWithReducedScopeAsync(agentRole, state);
        }
        else if (state.RetryCount >= _options.MaxRetries)
        {
            await EscalateToHumanAsync(agentRole, state);
        }
    }

    private async Task RestartWithCheckpointAsync(string agentRole, AgentState state)
    {
        var resumptionContext = await _checkpointService.GenerateResumptionContextAsync(agentRole);

        state.Status = AgentStatus.Queued;
        state.RecoveryContext = resumptionContext;
        await _stateManager.SaveAgentStateAsync(state);
    }

    private async Task EscalateToHumanAsync(string agentRole, AgentState state)
    {
        state.Status = AgentStatus.Escalated;
        await _stateManager.SaveAgentStateAsync(state);

        var checkpoint = await _checkpointService.GetLatestCheckpointAsync(agentRole);
        var notification = EscalationNotification.FromAgentState(state, checkpoint);

        await _notificationService.SendEscalationAsync(notification);
    }
}
```

### Heartbeat Monitoring

```csharp
public class HeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastHeartbeats = new();

    public void RecordHeartbeat(string agentRole)
    {
        _lastHeartbeats[agentRole] = DateTime.UtcNow;
    }

    public bool IsAgentHealthy(string agentRole)
    {
        if (!_lastHeartbeats.TryGetValue(agentRole, out var lastHeartbeat))
            return false;

        var silentDuration = DateTime.UtcNow - lastHeartbeat;
        return silentDuration < _options.HeartbeatTimeout;
    }

    public Task<IReadOnlyList<string>> GetUnhealthyAgentsAsync()
    {
        var unhealthy = _lastHeartbeats
            .Where(kvp => !IsAgentHealthy(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(unhealthy);
    }
}
```

---

## Context Limit Management

### Checkpoint Service

```csharp
public class ContextCheckpointService : IContextCheckpointService
{
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

            ### Progress: {checkpoint.PercentComplete:F0}%

            #### Completed:
            {FormatList(checkpoint.CompletedItemsJson, "[x]")}

            #### Remaining:
            {FormatList(checkpoint.PendingItemsJson, "[ ]")}

            ### Active Files
            {FormatList(checkpoint.ActiveFilesJson)}

            ### Notes
            {checkpoint.Notes ?? "None"}

            ---
            **Continue from this checkpoint. Do not repeat completed work.**
            """;
    }
}
```

### Task Decomposition

```csharp
public class TaskDecomposerService : ITaskDecomposerService
{
    public IReadOnlyList<WorkItem> DecomposeTask(WorkItem task)
    {
        var estimatedTokens = EstimateTaskTokens(task);

        if (estimatedTokens <= _options.SafeContextTokens)
        {
            return new[] { task };
        }

        // Split into smaller work items based on file count
        var subtasks = new List<WorkItem>();
        var currentBatch = new List<string>();
        var currentEstimate = 0;

        foreach (var file in task.Files)
        {
            if (currentEstimate + _options.TokensPerFile > _options.SafeContextTokens
                && currentBatch.Any())
            {
                subtasks.Add(CreateSubtask(task, currentBatch, subtasks.Count + 1));
                currentBatch = new List<string>();
                currentEstimate = 0;
            }

            currentBatch.Add(file);
            currentEstimate += _options.TokensPerFile;
        }

        if (currentBatch.Any())
        {
            subtasks.Add(CreateSubtask(task, currentBatch, subtasks.Count + 1));
        }

        return subtasks;
    }
}
```

---

## Data Models

### ProjectState

```csharp
public class ProjectState
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string WorkingDirectory { get; set; }
    public ProjectPhase Phase { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
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

### AgentState

```csharp
public class AgentState
{
    public int Id { get; set; }
    public required string Role { get; set; }
    public AgentStatus Status { get; set; }
    public required string SubagentType { get; set; }
    public DateTime? SpawnedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? TimeoutAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public string? TaskId { get; set; }
    public int RetryCount { get; set; }
    public string? ArtifactsJson { get; set; }      // JSON: ["file1.md", "file2.cs"]
    public string? DependenciesJson { get; set; }   // JSON: ["architect", "designer"]
    public string? LastMessage { get; set; }
    public string? LastError { get; set; }
    public int? EstimatedContextUsage { get; set; }
    public string? RecoveryContext { get; set; }    // Context for timeout restart
}
```

### AgentMessage

```csharp
public class AgentMessage
{
    public required string Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string From { get; set; }
    public required string To { get; set; }         // Role, "supervisor", or "all"
    public MessageType Type { get; set; }
    public required string Content { get; set; }
    public string? ArtifactsJson { get; set; }
    public string? MetadataJson { get; set; }
}

public enum MessageType
{
    Assignment,
    Progress,
    Question,
    Answer,
    Info,
    Request,
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

### Checkpoint

```csharp
public class Checkpoint
{
    public int Id { get; set; }
    public required string AgentRole { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public required string Summary { get; set; }
    public int CompletedTaskCount { get; set; }
    public int TotalTaskCount { get; set; }
    public double PercentComplete => TotalTaskCount > 0
        ? (double)CompletedTaskCount / TotalTaskCount * 100
        : 0;
    public string? CompletedItemsJson { get; set; }
    public string? PendingItemsJson { get; set; }
    public string? ActiveFilesJson { get; set; }
    public string? Notes { get; set; }
    public int? EstimatedContextUsage { get; set; }
}
```

### Supporting Models

```csharp
public record WorkItem
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
    public string? AssignedAgent { get; init; }
}

public record SpawnResult
{
    public required string TaskId { get; init; }
    public int ProcessId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static SpawnResult Succeeded(string taskId, int processId) =>
        new() { TaskId = taskId, ProcessId = processId, Success = true };

    public static SpawnResult Failed(string taskId, string errorMessage) =>
        new() { TaskId = taskId, ProcessId = 0, Success = false, ErrorMessage = errorMessage };
}

public record AgentProcessInfo
{
    public int ProcessId { get; init; }
    public required string AgentRole { get; init; }
    public DateTime StartedAt { get; init; }
    public AgentProcessStatus Status { get; init; }
    public int? ExitCode { get; init; }
}

public enum AgentProcessStatus
{
    Running,
    Exited,
    Terminated,
    Unknown
}

public record EscalationNotification
{
    public required string AgentRole { get; init; }
    public int FailureCount { get; init; }
    public string? LastError { get; init; }
    public Checkpoint? Checkpoint { get; init; }
    public IReadOnlyList<string> Artifacts { get; init; } = [];
    public string? Context { get; init; }

    public static EscalationNotification FromAgentState(
        AgentState agent,
        Checkpoint? checkpoint,
        string? additionalContext = null);
}
```

---

## Configuration Reference

### Root Configuration (ApmasOptions)

```json
{
  "Apmas": {
    "ProjectName": "my-project",
    "WorkingDirectory": "C:/projects/my-project",
    "DataDirectory": ".apmas",
    "Timeouts": { ... },
    "Agents": { ... },
    "Spawner": { ... },
    "HttpTransport": { ... },
    "Decomposition": { ... },
    "Notifications": { ... },
    "Metrics": { ... }
  }
}
```

### TimeoutOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| DefaultMinutes | int | 30 | Default agent timeout |
| HeartbeatIntervalMinutes | int | 5 | Expected heartbeat frequency |
| HeartbeatTimeoutMinutes | int | 10 | Max time without heartbeat |
| MaxRetries | int | 3 | Max retry attempts before escalation |
| PollingIntervalSeconds | int | 30 | Supervisor polling frequency |
| AgentOverrides | Dictionary | {} | Per-agent timeout overrides |

```json
"Timeouts": {
  "DefaultMinutes": 30,
  "HeartbeatIntervalMinutes": 5,
  "HeartbeatTimeoutMinutes": 10,
  "MaxRetries": 3,
  "PollingIntervalSeconds": 30,
  "AgentOverrides": {
    "architect": 15,
    "developer": 45,
    "reviewer": 20
  }
}
```

### AgentOptions

```json
"Agents": {
  "Roster": [
    {
      "Role": "architect",
      "SubagentType": "systems-architect",
      "Dependencies": [],
      "Description": "Designs system architecture",
      "PromptType": "ArchitectPrompt"
    },
    {
      "Role": "designer",
      "SubagentType": "design-specialist",
      "Dependencies": [],
      "PromptType": "DesignerPrompt"
    },
    {
      "Role": "developer",
      "SubagentType": "html-prototyper",
      "Dependencies": ["architect", "designer"],
      "PromptType": "DeveloperPrompt"
    },
    {
      "Role": "reviewer",
      "SubagentType": "code-reviewer",
      "Dependencies": ["developer"],
      "PromptType": "ReviewerPrompt"
    }
  ]
}
```

### SpawnerOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| ClaudeCodePath | string | "claude" | Path to claude CLI |
| Model | string | "sonnet" | Model to use |
| MaxTurns | int | 100 | Max turns before auto-stop |
| McpConfigPath | string? | null | MCP config file path |
| UseHttpTransport | bool | true | Use HTTP instead of stdio |
| GracefulShutdownTimeoutMs | int | 5000 | Graceful termination timeout |
| DangerouslySkipPermissions | bool | true | Skip permission checks |
| OutputFormat | string | "stream-json" | CLI output format |
| LogAgentOutput | bool | true | Log agent stdout/stderr |

```json
"Spawner": {
  "ClaudeCodePath": "claude",
  "Model": "sonnet",
  "MaxTurns": 100,
  "UseHttpTransport": true,
  "GracefulShutdownTimeoutMs": 5000,
  "DangerouslySkipPermissions": true,
  "OutputFormat": "stream-json"
}
```

### HttpTransportOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| Enabled | bool | true | Enable HTTP transport |
| Port | int | 5050 | HTTP server port |
| Host | string | "localhost" | Bind address |
| SseKeepAliveSeconds | int | 30 | SSE keep-alive interval |
| MaxRequestBodySize | long | 10MB | Max request size |

```json
"HttpTransport": {
  "Enabled": true,
  "Port": 5050,
  "Host": "127.0.0.1",
  "SseKeepAliveSeconds": 30,
  "MaxRequestBodySize": 10485760
}
```

**Security Note**: Default binds to localhost only. Using "0.0.0.0" exposes the server to the network - use a reverse proxy in production.

### DecompositionOptions

```json
"Decomposition": {
  "SafeContextTokens": 50000,
  "TokensPerFile": 15000
}
```

### NotificationOptions

```json
"Notifications": {
  "Provider": "Console",
  "Email": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": null,
    "Password": null,
    "FromAddress": "apmas@example.com",
    "FromName": "APMAS",
    "ToAddresses": ["team@example.com"]
  },
  "Slack": {
    "WebhookUrl": "https://hooks.slack.com/services/...",
    "Channel": "#alerts",
    "Username": "APMAS",
    "IconEmoji": ":robot_face:"
  }
}
```

**Provider options**: `Console`, `Email`, `Slack`

**Security Note**: Never store passwords in appsettings.json. Use User Secrets (dev) or Key Vault (prod).

### MetricsOptions

```json
"Metrics": {
  "Enabled": true,
  "OpenTelemetry": {
    "Enabled": false,
    "Endpoint": "http://localhost:4317",
    "Protocol": "grpc"
  }
}
```

---

## Transport Mechanisms

### HTTP/SSE MCP Transport

The primary transport mechanism for spawned agents. Implemented in `HttpMcpServerHost.cs`.

**Protocol**: MCP over HTTP with Server-Sent Events
- MCP version: "2025-11-25"
- Server name: "APMAS"
- Server version: "1.0.0"

**Connection Flow**:
1. APMAS starts `HttpMcpServerHost` on configured port (default 5050)
2. Supervisor spawns Claude Code agent with MCP config pointing to HTTP endpoint
3. Agent sends MCP initialize request via HTTP POST
4. Server establishes SSE connection for server-to-client streaming
5. Agent calls tools via HTTP POST to `/tool/{toolName}`
6. Agent reads resources via HTTP GET to `/resource?uri={uri}`
7. SSE keep-alive comments sent every 30 seconds
8. Agent calls `apmas_complete` when done
9. Connection closed

**Features**:
- Configurable host and port
- Request size limits (default 10MB)
- SSE keep-alive heartbeats
- Connection state management
- Graceful shutdown coordination

### Agent Process Management

```csharp
public class ClaudeCodeSpawner : IAgentSpawner
{
    public async Task<SpawnResult> SpawnAgentAsync(
        string role,
        string subagentType,
        string? checkpointContext = null)
    {
        // Build command: claude --model sonnet --max-turns 100 ...
        // Set environment variables including MCP endpoint
        // Start process and track in _processes dictionary
        // Return TaskId and ProcessId
    }

    public async Task ShutdownAllAgentsAsync()
    {
        // Close stdin (EOF signal)
        // Wait graceful timeout (default 5s)
        // Force kill entire process tree if needed
    }
}
```

---

## Storage Layer

### IStateStore Interface

```csharp
public interface IStateStore
{
    Task EnsureStorageCreatedAsync();

    // Project state
    Task<ProjectState?> GetProjectStateAsync();
    Task SaveProjectStateAsync(ProjectState state);

    // Agent state
    Task<AgentState?> GetAgentStateAsync(string role);
    Task<IReadOnlyList<AgentState>> GetAllAgentStatesAsync();
    Task SaveAgentStateAsync(AgentState state);

    // Checkpoints
    Task<Checkpoint?> GetLatestCheckpointAsync(string agentRole);
    Task<IReadOnlyList<Checkpoint>> GetCheckpointHistoryAsync(string agentRole, int limit = 10);
    Task SaveCheckpointAsync(Checkpoint checkpoint);

    // Messages
    Task<IReadOnlyList<AgentMessage>> GetMessagesAsync(
        string? agentRole = null,
        DateTime? since = null,
        int? limit = null);
    Task SaveMessageAsync(AgentMessage message);
}
```

### SQLite Implementation

**Database**: `.apmas/state.db`

**Tables**:
- `ProjectStates` - Single row for project metadata
- `AgentStates` - One row per agent
- `Checkpoints` - Multiple per agent, ordered by creation time
- `AgentMessages` - All inter-agent communication

**Initialization**:
- `EnsureStorageCreatedAsync()` creates tables if needed
- EF Core migrations handle schema updates

---

## Monitoring & Observability

### Structured Logging with Serilog

```csharp
builder.Services.AddSerilog((services, config) => config
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "APMAS")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .WriteTo.File(
        path: ".apmas/logs/apmas-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7));
```

### Metrics Collection

```csharp
public class ApmasMetrics : IApmasMetrics
{
    private readonly Counter<int> _agentsSpawned;
    private readonly Counter<int> _agentsCompleted;
    private readonly Counter<int> _agentsFailed;
    private readonly Counter<int> _agentsTimedOut;
    private readonly Histogram<double> _agentDuration;

    public void RecordAgentSpawned(string role);
    public void RecordAgentCompleted(string role, TimeSpan duration);
    public void RecordAgentFailed(string role, string reason);
    public void RecordAgentTimedOut(string role);
}
```

### Health Check

```csharp
public class ApmasHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var projectState = await _stateManager.GetProjectStateAsync();
        var unhealthyAgents = await _heartbeatMonitor.GetUnhealthyAgentsAsync();

        if (unhealthyAgents.Any())
            return HealthCheckResult.Degraded($"Unhealthy agents: {string.Join(", ", unhealthyAgents)}");

        if (projectState?.Phase == ProjectPhase.Failed)
            return HealthCheckResult.Unhealthy("Project in failed state");

        return HealthCheckResult.Healthy("APMAS is running normally");
    }
}
```

---

## Agent Prompts

### Prompt Factory

```csharp
public interface IPromptFactory
{
    BaseAgentPrompt CreatePrompt(string promptType);
}

public class PromptFactory : IPromptFactory
{
    public BaseAgentPrompt CreatePrompt(string promptType) => promptType switch
    {
        "ArchitectPrompt" => new ArchitectPrompt(),
        "DesignerPrompt" => new DesignerPrompt(),
        "DeveloperPrompt" => new DeveloperPrompt(),
        "ReviewerPrompt" => new ReviewerPrompt(),
        "TesterPrompt" => new TesterPrompt(),
        _ => throw new ArgumentException($"Unknown prompt type: {promptType}")
    };
}
```

### Base Agent Prompt

```csharp
public abstract class BaseAgentPrompt
{
    public abstract string Role { get; }
    public abstract string SubagentType { get; }

    public string Generate(ProjectState project, string? recoveryContext = null)
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
               apmas_heartbeat(agentRole: "{Role.ToLower()}", status: "working", progress: "current task")
               ```

            2. **Checkpoint (after each subtask)**
               Call `apmas_checkpoint` to save progress for recovery.
               ```
               apmas_checkpoint(
                 agentRole: "{Role.ToLower()}",
                 summary: "Completed X",
                 completedItems: ["item1", "item2"],
                 pendingItems: ["item3"]
               )
               ```

            3. **Completion**
               Call `apmas_complete` when ALL work is done.
               ```
               apmas_complete(
                 agentRole: "{Role.ToLower()}",
                 summary: "Summary of work",
                 artifacts: ["file1.md", "file2.cs"]
               )
               ```

            ### Context Management

            If you feel your context filling up:
            1. Call `apmas_checkpoint` with your current progress
            2. Call `apmas_report_status(status: "context_limit", message: "Approaching limits")`
            3. Stop work - the Supervisor will respawn you with your checkpoint

            ## Your Task
            {GetTaskDescription()}

            ## Deliverables
            {GetDeliverables()}

            {(recoveryContext != null ? $"## Recovery Context\n{recoveryContext}" : "")}

            ---

            **BEGIN:** Start your work now. Call `apmas_heartbeat` every 5 minutes.
            """;
    }

    protected abstract string GetRoleDescription();
    protected abstract string GetTaskDescription();
    protected abstract string GetDeliverables();
}
```

### Available Prompts

| Prompt | Role | SubagentType |
|--------|------|--------------|
| ArchitectPrompt | Architect | systems-architect |
| DesignerPrompt | Designer | design-specialist |
| DeveloperPrompt | Developer | html-prototyper / dotnet-specialist |
| ReviewerPrompt | Reviewer | code-reviewer |
| TesterPrompt | Tester | test-writer |

---

## Startup Sequence

1. **Bootstrap Logger**: Console and Seq only, before DI setup
2. **Configuration**: Load from appsettings.json
3. **DI Registration**: All services, tools, resources
4. **Database Initialization**: Create tables if needed
5. **Project Initialization**: Create project and agents from config if not exists
6. **Dependency Validation**: Check for cycles and missing dependencies
7. **Start Background Services**:
   - HttpMcpServerHost (listens for agent connections)
   - SupervisorService (begins polling loop)
8. **Running**: Process agents, monitor heartbeats, handle timeouts
9. **Shutdown**: Gracefully terminate all agents

---

## Error Handling

### Tool Error Contract

- Return `ToolResult.Error(message)` for **expected errors** (validation, missing data)
- **Throw exceptions** for **unexpected errors** (bugs)
- HttpMcpServerHost catches thrown exceptions and returns MCP error response

### Graceful Shutdown

On application stop:
1. SupervisorService stops polling
2. ClaudeCodeSpawner.ShutdownAllAgentsAsync() called
3. Each agent: close stdin, wait timeout, force kill if needed
4. Database connections closed

---

## Security Considerations

1. **HTTP Binding**: Default localhost-only
2. **Request Size**: Limited to 10MB by default
3. **Agent Permissions**: `DangerouslySkipPermissions = true` by default
4. **Secrets**: Use User Secrets (dev) or Key Vault (prod) for passwords
5. **Agent Isolation**: Each agent runs as separate process

---

*Document Version: 2.0*
*Last Updated: 2026-01-22*
*Based on implementation commit: 0e5d5a4*
