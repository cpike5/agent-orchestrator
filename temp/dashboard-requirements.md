# APMAS Real-Time Dashboard - Requirements

## Overview

Create a web-based dashboard that provides real-time visualization of APMAS agent orchestration. The dashboard should allow operators to monitor agent progress, view messages between agents, and track the overall project status without needing to read log files.

## User Stories

### US-1: Real-Time Agent Status
As an operator, I want to see the current status of all agents in real-time so that I can monitor orchestration progress at a glance.

**Acceptance Criteria:**
- Display all configured agents with their current status (Pending, Queued, Spawning, Running, Completed, Failed, etc.)
- Show status transitions as they happen (no page refresh needed)
- Display time elapsed since agent started
- Show retry count if agent has been restarted

### US-2: Agent Dependency Visualization
As an operator, I want to see the agent dependency graph so that I understand the execution order and what's blocking what.

**Acceptance Criteria:**
- Visual representation of agent dependencies
- Clear indication of which agents are waiting on dependencies
- Show completed vs pending dependencies

### US-3: Message Stream
As an operator, I want to see messages between agents in real-time so that I can understand what each agent is working on.

**Acceptance Criteria:**
- Live feed of agent messages (Progress, Done, Error, etc.)
- Filter messages by agent or message type
- Timestamp on each message
- Visual distinction between message types

### US-4: Checkpoint Progress
As an operator, I want to see checkpoint information so that I can track detailed progress within each agent's work.

**Acceptance Criteria:**
- Show latest checkpoint for each running agent
- Display percent complete from checkpoint data
- Show completed vs pending items count

### US-5: Project Overview
As an operator, I want to see overall project status so that I know the big picture.

**Acceptance Criteria:**
- Project name and phase
- Start time and elapsed duration
- Count of completed vs total agents
- Overall progress indicator

## Technical Constraints

1. **Integration**: Must integrate with existing APMAS HTTP server (port 5050)
2. **Real-Time**: Use Server-Sent Events (SSE) for push updates (pattern already exists in codebase)
3. **No External Dependencies**: Prefer vanilla HTML/CSS/JS or minimal dependencies
4. **Single Page**: Dashboard should be a single HTML page served by the APMAS server
5. **Read-Only**: Dashboard is view-only, no control actions needed for MVP

## Data Sources Available

From the exploration, these are available:
- `GET /status` - Current snapshot of project and agent states
- `IMessageBus.SubscribeAsync()` - Real-time message stream
- `IAgentStateManager` - Agent and project state queries
- `ApmasMetrics` - OpenTelemetry counters and gauges

## Out of Scope (MVP)

- Agent control actions (pause, restart, kill)
- Historical run comparison
- Log file viewing
- Authentication/authorization
- Mobile responsiveness (desktop-first)
