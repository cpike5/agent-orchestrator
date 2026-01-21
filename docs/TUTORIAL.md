# Tutorial: Building a C# Application with APMAS

This tutorial walks through using APMAS to orchestrate multiple Claude Code agents to build a simple C# console application.

## What We'll Build

A "Task Tracker" console application with:
- Add, list, and complete tasks
- JSON file persistence
- Input validation

## Prerequisites

Before starting, ensure you have:

1. **APMAS installed and built** - See [Getting Started](GETTING-STARTED.md)
2. **Claude Code CLI** - `npm install -g @anthropic-ai/claude-code`
3. **A target project directory** - Where the application will be created

```bash
# Create the target project directory
mkdir C:/projects/task-tracker
cd C:/projects/task-tracker

# Initialize a git repo (recommended)
git init
```

## Step 1: Configure APMAS

Create or edit `src/Apmas.Server/appsettings.json` in your APMAS installation:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning"
      }
    }
  },
  "Apmas": {
    "ProjectName": "task-tracker",
    "WorkingDirectory": "C:/projects/task-tracker",
    "DataDirectory": ".apmas",
    "Timeouts": {
      "DefaultMinutes": 30,
      "HeartbeatIntervalMinutes": 5,
      "MaxRetries": 3,
      "AgentOverrides": {
        "architect": 15,
        "developer": 45,
        "tester": 30
      }
    },
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": [],
          "Description": "Design the application architecture",
          "PromptType": "ArchitectPrompt"
        },
        {
          "Role": "developer",
          "SubagentType": "dotnet-specialist",
          "Dependencies": ["architect"],
          "Description": "Implement the application",
          "PromptType": "DeveloperPrompt"
        },
        {
          "Role": "tester",
          "SubagentType": "test-writer",
          "Dependencies": ["developer"],
          "Description": "Write unit tests",
          "PromptType": "TesterPrompt"
        }
      ]
    },
    "Spawner": {
      "ClaudeCodePath": "claude",
      "Model": "sonnet",
      "MaxTurns": 100,
      "DangerouslySkipPermissions": true
    },
    "Notifications": {
      "Provider": "Console"
    }
  }
}
```

### Understanding the Agent Roster

The roster defines three agents with dependencies:

```
architect (no dependencies)
    │
    ▼
developer (waits for architect)
    │
    ▼
tester (waits for developer)
```

- **architect** runs first with no dependencies
- **developer** waits until architect completes
- **tester** waits until developer completes

## Step 2: Create a Project Brief

Create a project brief file in your target directory that describes what to build:

```bash
# In C:/projects/task-tracker/
```

Create `PROJECT-BRIEF.md`:

```markdown
# Task Tracker Application

## Overview
A simple command-line task tracker application in C#.

## Requirements

### Functional Requirements
1. Add a new task with a title and optional description
2. List all tasks (showing status: pending/completed)
3. Mark a task as completed by ID
4. Delete a task by ID
5. Persist tasks to a JSON file

### Technical Requirements
- .NET 8 Console Application
- Clean architecture with separation of concerns
- JSON file storage (tasks.json)
- Input validation
- Error handling

### User Interface
Command-line interface with commands:
- `add "Task title" ["description"]`
- `list`
- `complete <id>`
- `delete <id>`
- `help`

### Example Usage
```
> add "Buy groceries" "Milk, eggs, bread"
Task #1 created: Buy groceries

> add "Call mom"
Task #2 created: Call mom

> list
  ID  Status     Title
  1   [ ]        Buy groceries
  2   [ ]        Call mom

> complete 1
Task #1 marked as completed

> list
  ID  Status     Title
  1   [x]        Buy groceries
  2   [ ]        Call mom
```

## Deliverables
- Working console application
- Unit tests for core functionality
- README with usage instructions
```

## Step 3: Start the APMAS Server

Open a terminal and start the APMAS MCP server:

```bash
cd agent-orchestrator
dotnet run --project src/Apmas.Server
```

You should see output like:

```
[14:30:00 INF] [Program] Starting APMAS server
[14:30:01 INF] [DependencyResolver] Dependency graph validation passed
[14:30:01 INF] [Program] APMAS server started successfully
```

The server is now ready to receive MCP connections from agents.

## Step 4: Understanding the Agent Workflow

When APMAS orchestrates your project, agents work in sequence:

### Phase 1: Architecture (architect agent)

The architect agent:
1. Reads PROJECT-BRIEF.md
2. Designs the application structure
3. Creates architecture documentation
4. Signals completion via `apmas_complete`

**Expected output:** `docs/architecture.md` with class diagrams and structure

### Phase 2: Implementation (developer agent)

The developer agent:
1. Reads architecture from Phase 1
2. Creates the .NET project
3. Implements all classes and logic
4. Signals completion via `apmas_complete`

**Expected output:** Full source code in `src/`

### Phase 3: Testing (tester agent)

The tester agent:
1. Reads implemented code
2. Creates test project
3. Writes unit tests
4. Signals completion via `apmas_complete`

**Expected output:** Test project in `tests/`

## Step 5: Monitor Progress

### Using Seq (Recommended)

If you have Seq running at http://localhost:5341, you can watch real-time logs with structured queries:

```
Application = "APMAS" and AgentRole = "architect"
```

### Using Log Files

Check the log files in your project directory:

```bash
# View recent logs
tail -f C:/projects/task-tracker/.apmas/logs/apmas-*.log

# Filter for specific agent
grep "developer" C:/projects/task-tracker/.apmas/logs/apmas-*.log
```

### Checking Agent Status

The `.apmas/state.db` SQLite database contains current state. You can query it:

```bash
sqlite3 C:/projects/task-tracker/.apmas/state.db "SELECT Role, Status FROM AgentStates"
```

## Step 6: Review Results

After all agents complete, your project directory should contain:

```
task-tracker/
├── .apmas/                    # APMAS runtime data
│   ├── state.db
│   ├── logs/
│   └── checkpoints/
├── PROJECT-BRIEF.md           # Your input
├── docs/
│   └── architecture.md        # From architect
├── src/
│   └── TaskTracker/           # From developer
│       ├── TaskTracker.csproj
│       ├── Program.cs
│       ├── Models/
│       │   └── TaskItem.cs
│       ├── Services/
│       │   ├── ITaskService.cs
│       │   └── TaskService.cs
│       └── Storage/
│           ├── ITaskRepository.cs
│           └── JsonTaskRepository.cs
├── tests/
│   └── TaskTracker.Tests/     # From tester
│       ├── TaskTracker.Tests.csproj
│       └── TaskServiceTests.cs
└── README.md                  # Usage documentation
```

## Step 7: Build and Run

```bash
cd C:/projects/task-tracker

# Build the application
dotnet build src/TaskTracker

# Run the application
dotnet run --project src/TaskTracker

# Run tests
dotnet test tests/TaskTracker.Tests
```

## Handling Failures

### Agent Timeout

If an agent times out, APMAS automatically:
1. First timeout: Restarts with checkpoint context
2. Second timeout: Restarts with reduced scope
3. Third timeout: Escalates to human

Check the logs to see what happened:

```bash
grep -i "timeout\|failed" .apmas/logs/apmas-*.log
```

### Checkpoint Recovery

Agents save progress via `apmas_checkpoint`. If an agent is restarted, it receives its last checkpoint as context to continue where it left off.

View checkpoints:

```bash
ls -la .apmas/checkpoints/
cat .apmas/checkpoints/developer-latest.json
```

### Manual Intervention

If an agent is escalated, you'll see a console notification. You can:

1. Fix the issue manually
2. Reset the agent state (see [Troubleshooting](TROUBLESHOOTING.md))
3. Restart the APMAS server

## Customizing Agent Behavior

### Adjusting Timeouts

For complex tasks, increase agent timeouts:

```json
{
  "Timeouts": {
    "AgentOverrides": {
      "developer": 60,
      "tester": 45
    }
  }
}
```

### Using Different Models

For higher quality output, use the opus model:

```json
{
  "Spawner": {
    "Model": "opus"
  }
}
```

### Adding More Agents

Extend the roster for additional tasks:

```json
{
  "Agents": {
    "Roster": [
      // ... existing agents ...
      {
        "Role": "reviewer",
        "SubagentType": "code-reviewer",
        "Dependencies": ["developer"],
        "Description": "Review code quality"
      },
      {
        "Role": "docs",
        "SubagentType": "docs-writer",
        "Dependencies": ["developer"],
        "Description": "Write user documentation"
      }
    ]
  }
}
```

## Tips for Success

1. **Write Clear Briefs** - The more detailed your PROJECT-BRIEF.md, the better the results

2. **Start Simple** - Begin with 2-3 agents before adding complexity

3. **Check Checkpoints** - If something goes wrong, checkpoints show exactly where the agent stopped

4. **Monitor Heartbeats** - If an agent stops sending heartbeats, it may be stuck

5. **Review Architecture First** - Before developer starts, review the architect's output

## Example: Minimal Two-Agent Setup

For very simple projects, use just architect and developer:

```json
{
  "Apmas": {
    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "Dependencies": []
        },
        {
          "Role": "developer",
          "SubagentType": "dotnet-specialist",
          "Dependencies": ["architect"]
        }
      ]
    }
  }
}
```

## Next Steps

- [Configuration Reference](CONFIGURATION.md) - Full configuration options
- [Troubleshooting](TROUBLESHOOTING.md) - Common issues and solutions
- [APMAS Specification](../APMAS-SPEC.md) - Technical details

## Appendix: MCP Tool Usage by Agents

Agents use these MCP tools during execution:

| Tool | When Used |
|------|-----------|
| `apmas_heartbeat` | Every 5 minutes while working |
| `apmas_checkpoint` | After completing each subtask |
| `apmas_report_status` | When status changes significantly |
| `apmas_get_context` | To read other agents' outputs |
| `apmas_send_message` | To communicate with other agents |
| `apmas_request_help` | When blocked and need assistance |
| `apmas_complete` | When all work is finished |
