# Getting Started with APMAS

APMAS (Autonomous Project Management Agent System) is a .NET 8 MCP server that orchestrates multiple Claude Code agents to collaborate on software projects.

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 8 SDK** - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Claude Code CLI** - Install via npm: `npm install -g @anthropic-ai/claude-code`
- **Seq** (optional) - For local log aggregation: [Download Seq](https://datalust.co/seq)

Verify your installations:

```bash
dotnet --version    # Should show 8.x.x
claude --version    # Should show Claude Code version
```

## Installation

### Clone the Repository

```bash
git clone https://github.com/cpike5/agent-orchestrator.git
cd agent-orchestrator
```

### Build the Project

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

## Basic Configuration

APMAS uses `appsettings.json` for configuration. The minimal configuration requires:

1. **Project name** - Identifies your project
2. **Working directory** - Where the target project lives
3. **Agent roster** - Which agents to orchestrate

### Example Configuration

Create or modify `src/Apmas.Server/appsettings.json`:

```json
{
  "Apmas": {
    "ProjectName": "my-project",
    "WorkingDirectory": "C:/projects/my-project",
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": [],
          "Description": "System architecture and technical design"
        },
        {
          "Role": "developer",
          "SubagentType": "dotnet-specialist",
          "Dependencies": ["architect"],
          "Description": "Implementation and coding"
        }
      ]
    }
  }
}
```

## Running Your First Project

### 1. Start the MCP Server

```bash
cd agent-orchestrator
dotnet run --project src/Apmas.Server
```

The server will:
- Create a `.apmas` directory in your working directory for state, logs, and checkpoints
- Validate the agent dependency graph
- Start listening for MCP connections

### 2. Monitor Logs

If you have Seq running locally, open http://localhost:5341 to view structured logs.

Alternatively, check the log files in `.apmas/logs/`.

### 3. Understanding Agent Flow

APMAS manages agents through this lifecycle:

```
Pending → Queued → Spawning → Running → Completed
                      ↓          ↓
                   Failed     Paused (checkpoint)
                      ↓          ↓
                 Escalated   [restart with context]
```

1. **Pending** - Agent waiting for dependencies
2. **Queued** - Dependencies met, ready to spawn
3. **Spawning** - Agent being launched
4. **Running** - Agent actively working
5. **Completed** - Agent finished successfully

## Runtime Data

APMAS creates a `.apmas` directory in your project's working directory:

```
.apmas/
├── state.db           # SQLite database with project state
├── logs/              # Serilog log files
└── checkpoints/       # Agent progress snapshots
```

## MCP Tools

Agents communicate with APMAS using these MCP tools:

| Tool | Purpose |
|------|---------|
| `apmas_heartbeat` | Signal alive status (call every 5 min) |
| `apmas_checkpoint` | Save progress for recovery |
| `apmas_report_status` | Report task status and artifacts |
| `apmas_get_context` | Get project state and other agents' outputs |
| `apmas_send_message` | Send message to another agent |
| `apmas_request_help` | Request human or agent assistance |
| `apmas_complete` | Signal task completion |

## Next Steps

- [Configuration Reference](CONFIGURATION.md) - Full configuration options
- [Troubleshooting](TROUBLESHOOTING.md) - Common issues and solutions
- [APMAS Specification](../APMAS-SPEC.md) - Detailed technical specification

## Quick Reference

### Build Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run server
dotnet run --project src/Apmas.Server

# Run with watch mode
dotnet watch --project src/Apmas.Server
```
