# APMAS Real-Time Dashboard - Technical Specification

## 1. Architecture Overview

The dashboard integrates with the existing ASP.NET Core HTTP server on port 5050. It leverages existing infrastructure:

- **Existing `/status` endpoint** - Provides initial state snapshot
- **MessageBus.SubscribeAsync()** - Real-time message stream via System.Threading.Channels
- **AgentStateManager** - Agent and project state queries
- **SSE implementation** - Existing connection pooling and keep-alive in HttpMcpServerHost

### Design Principles

1. **Read-only MVP** - No control actions (pause/restart/kill)
2. **Vanilla HTML/CSS/JS** - No build step, served as static file
3. **Progressive enhancement** - Works without JavaScript (initial state), enhanced with real-time updates
4. **Single page** - All functionality on one HTML page

## 2. New HTTP Endpoints

### 2.1 Static Dashboard Endpoint

```
GET /dashboard
```

Serves the dashboard HTML file as embedded resource from assembly.

**Implementation:**
- Embed `dashboard.html` as resource in Apmas.Server project
- Add endpoint in `HttpMcpServerHost.ExecuteAsync()`:
  ```csharp
  _app.MapGet("/dashboard", HandleDashboardAsync);
  ```
- Return HTML with `Content-Type: text/html; charset=utf-8`

### 2.2 Dashboard Events SSE Endpoint

```
GET /dashboard/events
```

Server-Sent Events stream for real-time dashboard updates.

**Event Types:**
- `agent-update` - Agent status changed
- `message` - New inter-agent message
- `checkpoint` - Agent checkpoint created
- `project-update` - Project phase or state changed

**Event Data Format:**
```json
{
  "type": "agent-update",
  "timestamp": "2025-01-22T10:30:00Z",
  "data": {
    "role": "architect",
    "status": "Running",
    "lastHeartbeat": "2025-01-22T10:29:55Z",
    "retryCount": 0
  }
}
```

**Implementation:**
- Create `DashboardEventService` that subscribes to MessageBus
- Monitor AgentStateManager for state changes (polling every 2s or event-based if possible)
- Use existing SSE pattern from `HandleSseConnection()`
- Keep-alive comments every 30 seconds (reuse existing interval)

### 2.3 Dashboard API Endpoint

```
GET /api/dashboard/state
```

Complete dashboard state snapshot (superset of `/status`).

**Response Schema:**
```json
{
  "timestamp": "2025-01-22T10:30:00Z",
  "project": {
    "name": "My Project",
    "phase": "Building",
    "startedAt": "2025-01-22T10:00:00Z",
    "workingDirectory": "/path/to/project",
    "elapsedSeconds": 1800,
    "completedAgentCount": 2,
    "totalAgentCount": 5
  },
  "agents": [
    {
      "role": "architect",
      "status": "Completed",
      "subagentType": "systems-architect",
      "spawnedAt": "2025-01-22T10:01:00Z",
      "completedAt": "2025-01-22T10:15:00Z",
      "elapsedSeconds": 840,
      "lastMessage": "Architecture complete",
      "retryCount": 0,
      "dependencies": [],
      "lastCheckpoint": {
        "createdAt": "2025-01-22T10:14:00Z",
        "summary": "Completed architecture document",
        "percentComplete": 100,
        "completedTaskCount": 3,
        "totalTaskCount": 3
      }
    }
  ],
  "recentMessages": [
    {
      "id": "msg-123",
      "timestamp": "2025-01-22T10:15:00Z",
      "from": "architect",
      "to": "developer",
      "type": "Done",
      "content": "Architecture spec ready for implementation"
    }
  ],
  "agentDependencies": [
    {
      "agent": "developer",
      "dependsOn": ["architect"],
      "satisfied": true
    }
  ]
}
```

**Implementation:**
- Call `_stateManager.GetProjectStateAsync()`
- Call `_stateManager.GetAllAgentsAsync()`
- Call `_messageBus.GetAllMessagesAsync(limit: 50)` for recent messages
- For each agent, get latest checkpoint from `_store.GetLatestCheckpointAsync(agentRole)`
- Parse `DependenciesJson` from each agent to build dependency graph
- Calculate elapsed times, counts, and satisfaction status

## 3. Event Publication Strategy

### 3.1 Agent State Changes

Monitor agent state in existing locations where status updates occur:

1. **SupervisorService** - When spawning, completing, timing out agents
2. **HeartbeatMonitor** - When heartbeat received
3. **TimeoutHandler** - When retrying or escalating

**Approach:** Inject `IDashboardEventPublisher` into these services and publish events after state changes.

### 3.2 Message Events

Subscribe to `MessageBus.SubscribeAsync()` in `DashboardEventService` and republish as SSE events.

### 3.3 Checkpoint Events

Existing `apmas_checkpoint` tool handler in `CheckpointTool` should publish event after saving checkpoint.

### 3.4 Project Phase Changes

Monitor ProjectState updates in `AgentStateManager.UpdateProjectStateAsync()`.

## 4. Data Contracts

### 4.1 Dashboard Event (SSE)

```typescript
interface DashboardEvent {
  type: 'agent-update' | 'message' | 'checkpoint' | 'project-update';
  timestamp: string; // ISO 8601
  data: any; // Type-specific payload
}
```

### 4.2 Agent Update Event

```typescript
interface AgentUpdateEvent {
  role: string;
  status: AgentStatus;
  lastHeartbeat?: string;
  lastMessage?: string;
  lastError?: string;
  retryCount: number;
  elapsedSeconds?: number;
}
```

### 4.3 Message Event

```typescript
interface MessageEvent {
  id: string;
  timestamp: string;
  from: string;
  to: string;
  type: MessageType;
  content: string;
}
```

### 4.4 Checkpoint Event

```typescript
interface CheckpointEvent {
  agentRole: string;
  createdAt: string;
  summary: string;
  percentComplete: number;
  completedTaskCount: number;
  totalTaskCount: number;
}
```

### 4.5 Project Update Event

```typescript
interface ProjectUpdateEvent {
  name: string;
  phase: ProjectPhase;
  elapsedSeconds: number;
  completedAgentCount: number;
  totalAgentCount: number;
}
```

## 5. Frontend Implementation

### 5.1 Technology Stack

- **Vanilla HTML5/CSS3/JavaScript** (ES6+)
- **EventSource API** for SSE consumption
- **Fetch API** for initial state load
- **CSS Grid/Flexbox** for layout
- **No external dependencies** (no libraries, no build step)

### 5.2 Page Structure

```html
<!DOCTYPE html>
<html>
<head>
  <title>APMAS Dashboard</title>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <style>/* Embedded CSS */</style>
</head>
<body>
  <!-- Header: Project overview -->
  <header id="project-header"></header>

  <!-- Main grid: 2 columns -->
  <main>
    <!-- Left: Agent status cards + dependency graph -->
    <section id="agents-section">
      <div id="agent-cards"></div>
      <div id="dependency-graph"></div>
    </section>

    <!-- Right: Message stream + filters -->
    <section id="messages-section">
      <div id="message-filters"></div>
      <div id="message-stream"></div>
    </section>
  </main>

  <script>/* Embedded JavaScript */</script>
</body>
</html>
```

### 5.3 Visual Design

**Project Header:**
- Project name and current phase (large, prominent)
- Start time and elapsed duration
- Progress bar: completed agents / total agents
- Connection status indicator (green = connected, red = disconnected, yellow = reconnecting)

**Agent Cards:**
- Card per agent with color-coded status border:
  - Pending: Gray
  - Queued: Blue
  - Spawning: Cyan
  - Running: Green (pulsing animation)
  - Paused: Orange
  - Completed: Dark green
  - Failed: Red
  - TimedOut: Dark red
  - Escalated: Purple
- Show: Role, status, elapsed time, retry count (if > 0)
- Show latest checkpoint progress bar if available
- Dependencies list (grayed out if satisfied, highlighted if waiting)

**Dependency Graph:**
- Simple text-based representation:
  ```
  architect ✓
    └─> developer (waiting)
    └─> tester (waiting)
  ```
- Or SVG-based node graph for visual appeal (stretch goal)

**Message Stream:**
- Reverse chronological order (newest first)
- Each message shows: timestamp, from → to, type badge, content
- Color-coded type badges (Info=blue, Progress=green, Error=red, etc.)
- Filters: by agent (dropdown), by type (checkboxes)
- Auto-scroll on new message (with pause button)

### 5.4 JavaScript Architecture

```javascript
// State management
const state = {
  project: null,
  agents: new Map(), // role -> agent data
  messages: [],
  filters: { agent: null, types: [] }
};

// API client
async function loadInitialState() {
  const res = await fetch('/api/dashboard/state');
  const data = await res.json();
  state.project = data.project;
  data.agents.forEach(a => state.agents.set(a.role, a));
  state.messages = data.recentMessages;
  render();
}

// SSE subscription
let eventSource;
function subscribeToEvents() {
  eventSource = new EventSource('/dashboard/events');

  eventSource.addEventListener('agent-update', e => {
    const event = JSON.parse(e.data);
    updateAgent(event.data);
  });

  eventSource.addEventListener('message', e => {
    const event = JSON.parse(e.data);
    addMessage(event.data);
  });

  // ... other event types

  eventSource.onerror = () => {
    updateConnectionStatus('disconnected');
    // Reconnection is automatic via EventSource
  };
}

// Rendering
function render() {
  renderProjectHeader();
  renderAgentCards();
  renderDependencyGraph();
  renderMessageStream();
}

// ... individual render functions
```

### 5.5 Styling Guidelines

- **Responsive** - Desktop-first, minimum width 1024px
- **Dark theme** - Easier on eyes for monitoring dashboards
- **Monospace font** for timestamps and IDs
- **Status colors** consistent with terminal/CLI conventions
- **Smooth transitions** for state changes (0.3s ease)

## 6. Implementation Plan

### Phase 1: Backend Foundation (2-3 hours)

1. **Create `IDashboardEventPublisher` interface and `DashboardEventService` implementation**
   - File: `src/Apmas.Server/Core/Services/IDashboardEventPublisher.cs`
   - File: `src/Apmas.Server/Core/Services/DashboardEventService.cs`
   - Register in DI container with singleton lifetime

2. **Add `/api/dashboard/state` endpoint**
   - File: `src/Apmas.Server/Mcp/Http/HttpMcpServerHost.cs`
   - Method: `HandleDashboardStateAsync()`
   - Query all necessary data and build response

3. **Add `/dashboard/events` SSE endpoint**
   - File: `src/Apmas.Server/Mcp/Http/HttpMcpServerHost.cs`
   - Method: `HandleDashboardEventsAsync()`
   - Subscribe to `DashboardEventService` and stream events

4. **Integrate event publishing into existing services**
   - Inject `IDashboardEventPublisher` into:
     - `SupervisorService`
     - `HeartbeatMonitor`
     - `TimeoutHandler`
     - `AgentStateManager`
     - MCP tool handlers (CheckpointTool, ReportStatusTool)
   - Publish events at appropriate state transitions

### Phase 2: Frontend Development (3-4 hours)

5. **Create dashboard HTML file**
   - File: `src/Apmas.Server/wwwroot/dashboard.html` (embedded resource)
   - Implement full page structure and styling

6. **Implement JavaScript state management and rendering**
   - Initial state load from `/api/dashboard/state`
   - SSE subscription to `/dashboard/events`
   - Render functions for all UI sections
   - Message filtering logic

7. **Add `/dashboard` endpoint to serve HTML**
   - File: `src/Apmas.Server/Mcp/Http/HttpMcpServerHost.cs`
   - Method: `HandleDashboardAsync()`
   - Return embedded HTML resource

### Phase 3: Testing and Refinement (1-2 hours)

8. **Manual testing**
   - Start APMAS with test project configuration
   - Open dashboard in browser
   - Verify initial state renders correctly
   - Verify real-time updates work (spawn agents, send messages)
   - Test reconnection behavior (restart server while dashboard open)
   - Test filtering and UI interactions

9. **Performance validation**
   - Measure SSE event frequency under normal operation
   - Ensure dashboard doesn't impact supervisor performance
   - Test with 10+ agents and high message volume

10. **Documentation**
    - Update README.md with dashboard access instructions
    - Add dashboard screenshot
    - Document `/dashboard` and `/api/dashboard/state` endpoints

### Phase 4: Optional Enhancements (future)

- SVG-based dependency graph with D3.js or similar
- Historical run comparison (requires persistence)
- Log file viewer integration
- Export to JSON/CSV functionality
- Dark/light theme toggle
- Mobile-responsive layout

## 7. File Checklist

### New Files

| File | Purpose |
|------|---------|
| `src/Apmas.Server/Core/Services/IDashboardEventPublisher.cs` | Interface for publishing dashboard events |
| `src/Apmas.Server/Core/Services/DashboardEventService.cs` | Aggregates state changes and MessageBus into SSE events |
| `src/Apmas.Server/wwwroot/dashboard.html` | Single-page dashboard UI |

### Modified Files

| File | Changes |
|------|---------|
| `src/Apmas.Server/Mcp/Http/HttpMcpServerHost.cs` | Add 3 endpoints: `/dashboard`, `/api/dashboard/state`, `/dashboard/events` |
| `src/Apmas.Server/Core/Services/SupervisorService.cs` | Inject and call `IDashboardEventPublisher` on agent lifecycle changes |
| `src/Apmas.Server/Core/Services/HeartbeatMonitor.cs` | Publish event on heartbeat received |
| `src/Apmas.Server/Core/Services/TimeoutHandler.cs` | Publish event on timeout/retry/escalation |
| `src/Apmas.Server/Core/Services/AgentStateManager.cs` | Publish event on project phase changes |
| `src/Apmas.Server/Mcp/Tools/CheckpointTool.cs` | Publish event after checkpoint saved |
| `src/Apmas.Server/Mcp/Tools/ReportStatusTool.cs` | Publish event after status reported |
| `src/Apmas.Server/Program.cs` | Register `DashboardEventService` in DI |
| `src/Apmas.Server/Apmas.Server.csproj` | Add `dashboard.html` as embedded resource |

## 8. Configuration Options

Add to `ApmasOptions` (optional, can be hardcoded for MVP):

```csharp
public class DashboardOptions
{
    /// <summary>Enable the dashboard endpoints.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum messages to return in /api/dashboard/state.</summary>
    public int MaxRecentMessages { get; set; } = 50;

    /// <summary>SSE event buffer size.</summary>
    public int EventBufferSize { get; set; } = 100;
}
```

## 9. Security Considerations

**MVP (localhost only):**
- No authentication required
- Server binds to `localhost` only (existing behavior)
- Dashboard and API endpoints accessible to anyone with localhost access

**Future (production):**
- Add API key or basic auth
- CORS headers for cross-origin access
- Rate limiting on SSE endpoint

## 10. Performance Considerations

- **SSE connection limit:** Each browser has max 6 SSE connections per domain. Dashboard uses only 1.
- **Event throttling:** DashboardEventService should debounce rapid state changes (e.g., heartbeat every 5 min, not every second)
- **Message history:** Limit to 50-100 most recent messages to avoid unbounded growth
- **Channel backpressure:** Use bounded channel with drop oldest strategy if dashboard can't keep up

## 11. Testing Strategy

**Manual Test Scenarios:**

1. **Initial Load**
   - Navigate to `http://localhost:5050/dashboard`
   - Verify project info, all agents, and recent messages display

2. **Real-Time Agent Updates**
   - Trigger agent spawn (via supervisor)
   - Verify agent card appears and status updates in real-time

3. **Real-Time Messages**
   - Agent sends message via `apmas_send_message`
   - Verify message appears in stream within 1 second

4. **Checkpoint Progress**
   - Agent calls `apmas_checkpoint`
   - Verify progress bar updates on agent card

5. **Connection Resilience**
   - Restart APMAS server while dashboard is open
   - Verify EventSource reconnects automatically
   - Verify dashboard reloads state and resumes updates

6. **Filtering**
   - Select agent filter
   - Verify only messages involving that agent display
   - Select message type filter
   - Verify only messages of that type display

## 12. Success Criteria

1. Dashboard loads initial state in < 500ms
2. SSE events appear in dashboard within 1 second of state change
3. Dashboard handles 100+ messages without performance degradation
4. Dashboard reconnects automatically if server restarts
5. All 5 user stories from requirements are satisfied
6. No impact on supervisor performance (< 5% CPU overhead)

## 13. Future Enhancements

- **Agent control actions** (pause, resume, kill) - requires button UI and POST endpoints
- **Time-series charts** for agent activity over time (Chart.js or similar)
- **Artifact viewer** to browse agent outputs directly in dashboard
- **Alert thresholds** (notify if agent hasn't sent heartbeat in 10 min)
- **Multi-project support** if APMAS orchestrates multiple projects
- **Websocket upgrade** for bi-directional control (instead of read-only SSE)
