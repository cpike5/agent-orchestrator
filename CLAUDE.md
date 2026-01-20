# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

APMAS (Autonomous Project Management Agent System) is a .NET 8 MCP server that orchestrates multiple Claude Code agents. The key architectural insight: **agents cannot self-coordinate reliably** - an external MCP server must manage agent lifecycle, dependencies, and communication.

## Build and Test Commands

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run the MCP server
dotnet run --project src/Apmas.Server

# Watch mode during development
dotnet watch --project src/Apmas.Server
```

## Architecture

### Core Services (in `Core/Services/`)

| Service | Responsibility |
|---------|----------------|
| `SupervisorService` | Background service that monitors heartbeats, detects timeouts, manages agent lifecycle |
| `AgentStateManager` | Single source of truth for project and agent state |
| `MessageBus` | Inter-agent communication with guaranteed delivery |
| `AgentSpawner` | Launches Claude Code agents via CLI |
| `ContextCheckpointService` | Saves/restores agent progress for context limit recovery |

### MCP Tools (in `Mcp/Tools/`)

Agents use these tools to communicate with the orchestrator:

| Tool | Purpose |
|------|---------|
| `apmas_heartbeat` | Signal alive status (call every 5 min) |
| `apmas_checkpoint` | Save progress for recovery |
| `apmas_report_status` | Report task status and artifacts |
| `apmas_get_context` | Get project state and other agents' outputs |
| `apmas_send_message` | Send message to another agent |
| `apmas_request_help` | Request human or agent assistance |
| `apmas_complete` | Signal task completion |

### Agent Lifecycle States

```
Pending → Queued → Spawning → Running → Completed
                      ↓          ↓
                   Failed     Paused (checkpoint)
                      ↓          ↓
                 Escalated   [restart with context]
```

### Data Flow

1. Supervisor resolves dependencies and spawns agents
2. Agents call `apmas_heartbeat` every 5 minutes while working
3. Agents call `apmas_checkpoint` after completing subtasks
4. On timeout: Supervisor restarts agent with checkpoint context
5. After 3 failures: Escalate to human

## Project Structure

```
src/Apmas.Server/
├── Configuration/     # ApmasOptions, TimeoutPolicies
├── Core/
│   ├── Services/      # SupervisorService, AgentStateManager, MessageBus, AgentSpawner
│   ├── Models/        # ProjectState, AgentState, AgentMessage, Checkpoint, WorkItem
│   └── Enums/         # AgentStatus, MessageType, ProjectPhase
├── Mcp/
│   ├── Tools/         # MCP tool handlers
│   └── Resources/     # MCP resource handlers
├── Agents/
│   ├── Prompts/       # Agent prompt templates
│   └── Definitions/   # AgentRoster configuration
└── Storage/           # IStateStore implementations (SQLite, File)
```

### Runtime Data (`.apmas/` in target project)

```
.apmas/
├── state.db           # SQLite database
├── logs/              # Serilog output
└── checkpoints/       # Agent progress snapshots
```

## Key Design Patterns

- **Options pattern** (`IOptions<ApmasOptions>`) for all configuration
- **BackgroundService** for the Supervisor polling loop
- **Record types** for immutable state models
- **Serilog + Seq** for structured logging and local log aggregation

## Timeout Strategy

1. First timeout → Restart with checkpoint context
2. Second timeout → Restart with reduced scope
3. Third timeout → Escalate to human

## Specification Reference

Full specification is in [APMAS-SPEC.md](APMAS-SPEC.md) including:
- Detailed C# interface definitions
- MCP tool JSON schemas
- Agent prompt templates
- Configuration file format
