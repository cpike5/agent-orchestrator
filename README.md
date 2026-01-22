# APMAS - Autonomous Project Management Agent System

A .NET 8 MCP server that orchestrates multiple Claude Code agents to collaborate on software projects.

## Overview

APMAS addresses three critical challenges in multi-agent coordination:

1. **Agent Timeouts** - Agents that hang or take too long
2. **Context Limits** - Agents exhausting their context window
3. **Monitoring & Restart** - Detecting failures and recovering gracefully

The key architectural insight: **agents cannot self-coordinate reliably**. An external orchestrator (the MCP server) must manage agent lifecycle, dependencies, and communication.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      APMAS MCP Server (.NET 8)                  │
│  ┌───────────┐  ┌───────────┐  ┌───────────┐  ┌───────────┐    │
│  │ Supervisor│  │  State    │  │  Message  │  │  Agent    │    │
│  │  Service  │  │  Manager  │  │   Bus     │  │  Spawner  │    │
│  └───────────┘  └───────────┘  └───────────┘  └───────────┘    │
│                         MCP Tool Handlers                       │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │           HTTP/SSE Transport (localhost:5050)            │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
              │ HTTP/SSE          │ HTTP/SSE          │ HTTP/SSE
              ▼                   ▼                   ▼
    ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
    │  Claude Agent   │ │  Claude Agent   │ │  Claude Agent   │
    │   (Architect)   │ │   (Developer)   │ │   (Reviewer)    │
    └─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Quick Start

### Prerequisites

- .NET 8 SDK
- Claude Code CLI (`npm install -g @anthropic-ai/claude-code`)
- Seq (optional, for log aggregation)

### Installation

```bash
git clone https://github.com/cpike5/agent-orchestrator.git
cd agent-orchestrator
dotnet build
```

### Configure

Edit `src/Apmas.Server/appsettings.json`:

```json
{
  "Apmas": {
    "ProjectName": "my-project",
    "WorkingDirectory": "C:/projects/my-project",
    "HttpTransport": {
      "Port": 5050,
      "Host": "127.0.0.1"
    },
    "Agents": {
      "Roster": [
        { "Role": "architect", "SubagentType": "systems-architect", "Dependencies": [] },
        { "Role": "developer", "SubagentType": "dotnet-specialist", "Dependencies": ["architect"] }
      ]
    }
  }
}
```

### Run

```bash
dotnet run --project src/Apmas.Server
```

## Features

- **HTTP/SSE Transport** - Agents connect via MCP HTTP protocol with Server-Sent Events
- **Dependency Resolution** - Agents only start when their dependencies complete
- **Heartbeat Monitoring** - Detects unresponsive agents based on configurable timeouts
- **Automatic Recovery** - Restarts failed agents with checkpoint context
- **Progressive Retry** - Checkpoint recovery, scope reduction, then human escalation
- **Graceful Shutdown** - Closes stdin, waits timeout, force kills if needed
- **Notifications** - Console, Email, or Slack escalation notifications
- **Structured Logging** - Serilog with Seq integration

## MCP Tools

Agents communicate with APMAS using these tools:

| Tool | Purpose |
|------|---------|
| `apmas_heartbeat` | Signal alive status |
| `apmas_checkpoint` | Save progress for recovery |
| `apmas_report_status` | Report task status |
| `apmas_get_context` | Get project state |
| `apmas_send_message` | Message other agents |
| `apmas_request_help` | Request assistance |
| `apmas_complete` | Signal completion |

## Documentation

- [Getting Started](docs/GETTING-STARTED.md) - Installation and first project
- [Tutorial: Build a C# App](docs/TUTORIAL.md) - Step-by-step walkthrough
- [Configuration Reference](docs/CONFIGURATION.md) - All configuration options
- [Troubleshooting](docs/TROUBLESHOOTING.md) - Common issues and solutions
- [APMAS Specification](APMAS-SPEC.md) - Detailed technical specification

## Development

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run with watch mode
dotnet watch --project src/Apmas.Server
```

## Project Structure

```
src/Apmas.Server/
├── Configuration/     # ApmasOptions, TimeoutOptions, HttpTransportOptions, etc.
├── Core/
│   ├── Services/      # SupervisorService, AgentStateManager, MessageBus, HeartbeatMonitor
│   ├── Models/        # ProjectState, AgentState, Checkpoint, WorkItem
│   └── Enums/         # AgentStatus, MessageType, ProjectPhase
├── Mcp/
│   ├── Tools/         # MCP tool handlers
│   ├── Resources/     # MCP resource handlers
│   └── Http/          # HttpMcpServerHost, IHttpServerReadySignal
├── Agents/
│   ├── Prompts/       # Agent prompt classes (C#) and IPromptFactory
│   ├── Definitions/   # AgentRoster configuration
│   └── ClaudeCodeSpawner.cs
└── Storage/           # IStateStore, SqliteStateStore, ApmasDbContext
```

## License

MIT
