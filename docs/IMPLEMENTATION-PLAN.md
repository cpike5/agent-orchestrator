# APMAS Implementation Plan

This document outlines the implementation strategy for the Autonomous Project Management Agent System.

## Overview

The implementation is divided into 4 phases, each building on the previous. The goal is to have a working MCP server that can orchestrate Claude Code agents with proper timeout handling, context recovery, and monitoring.

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 8 |
| Database | SQLite (via EF Core) |
| Logging | Serilog + Seq |
| MCP Protocol | stdio transport |
| Metrics | System.Diagnostics.Metrics |
| Testing | xUnit + FluentAssertions |

## Phase 1: Core Infrastructure

**Goal:** Establish the foundation - project structure, data models, and state persistence.

### 1.1 Project Setup
- Create .NET 8 solution structure
- Configure dependency injection
- Set up Serilog logging with Seq sink
- Add appsettings.json with ApmasOptions

### 1.2 Data Models
- Implement record types: `ProjectState`, `AgentState`, `AgentMessage`, `Checkpoint`, `WorkItem`
- Implement enums: `AgentStatus`, `MessageType`, `ProjectPhase`
- Create EF Core DbContext for SQLite
- Add initial migration

### 1.3 State Management
- Implement `IStateStore` interface
- Implement `SqliteStateStore`
- Implement `AgentStateManager`
- Implement `MessageBus` for inter-agent messaging

## Phase 2: MCP Server

**Goal:** Implement the MCP protocol layer and tool handlers.

### 2.1 MCP Protocol Implementation
- Implement MCP server base with stdio transport
- Handle JSON-RPC message framing
- Implement capability negotiation

### 2.2 Tool Handlers
- `apmas_heartbeat` - Agent liveness signaling
- `apmas_report_status` - Status and artifact reporting
- `apmas_checkpoint` - Progress checkpointing
- `apmas_get_context` - Context retrieval
- `apmas_send_message` - Inter-agent messaging
- `apmas_request_help` - Help/escalation requests
- `apmas_complete` - Task completion signaling

### 2.3 MCP Resources
- Project state resource
- Agent messages resource
- Checkpoint resource

## Phase 3: Supervisor Service

**Goal:** Implement the orchestration brain - lifecycle management, timeout handling, and recovery.

### 3.1 Core Supervisor
- Implement `SupervisorService` as BackgroundService
- Add polling loop for health checks
- Implement dependency resolution (`DependencyResolver`)

### 3.2 Agent Spawner
- Implement `IAgentSpawner` interface
- Implement `ClaudeCodeSpawner` for CLI integration
- Handle process management (spawn, terminate, monitor)

### 3.3 Timeout Handling
- Implement `TimeoutHandler` with retry strategies
- Implement `HeartbeatMonitor`
- Add escalation logic (retry → reduced scope → human)

### 3.4 Context Recovery
- Implement `ContextCheckpointService`
- Add resumption context generation
- Implement `TaskDecomposer` for scope reduction

## Phase 4: Agent Integration

**Goal:** Create agent prompts, test end-to-end, and document.

### 4.1 Agent Prompts
- Implement `BaseAgentPrompt` template
- Create role-specific prompts (Architect, Designer, Developer, Reviewer, Tester)
- Add prompt injection for continuation context

### 4.2 End-to-End Testing
- Test full agent workflow
- Test timeout and recovery scenarios
- Test context limit handling
- Test multi-agent coordination

### 4.3 Observability
- Implement `ApmasMetrics` for metrics collection
- Implement `ApmasHealthCheck`
- Add structured logging throughout

## Deliverables by Phase

| Phase | Key Deliverables |
|-------|------------------|
| 1 | Solution structure, EF Core models, state persistence |
| 2 | Working MCP server with all 7 tools |
| 3 | Supervisor that spawns/monitors/restarts agents |
| 4 | Complete system with prompts and tests |

## Success Criteria

- [ ] Agents can communicate via MCP tools
- [ ] Supervisor detects and recovers from timeouts
- [ ] Checkpoints enable context recovery
- [ ] Human escalation works after max retries
- [ ] Metrics and logs provide visibility

## Dependencies

```
Phase 1 ──► Phase 2 ──► Phase 3 ──► Phase 4
              │            │
              └────────────┘
           (MCP tools need state)
```

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Claude Code CLI changes | Abstract spawner interface; integration tests |
| Context limits reached | Aggressive checkpointing; task decomposition |
| Agent hangs | Conservative heartbeat timeouts; process monitoring |
| State corruption | SQLite transactions; checkpoint validation |
