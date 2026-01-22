# Project Manager Orchestrator Architecture

**Status:** Draft
**Created:** 2026-01-22
**Parent:** [discord-pm-agent-design.md](discord-pm-agent-design.md)
**Purpose:** Evolve PM from one-shot analyzer to active work coordinator

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Architecture Comparison](#architecture-comparison)
3. [Core Concepts](#core-concepts)
4. [Data Models](#data-models)
5. [PM Coordinator Behavior](#pm-coordinator-behavior)
6. [Agent Modifications](#agent-modifications)
7. [New MCP Tools](#new-mcp-tools)
8. [Supervisor Changes](#supervisor-changes)
9. [Implementation Phases](#implementation-phases)
10. [Risk Analysis](#risk-analysis)

---

## Executive Summary

### Problem

Current APMAS uses a **sequential pipeline**:

```
Architect (all specs) → Designer (all designs) → Developer (all code) → Review
```

This wastes time. The Architect might spend 30 minutes writing specs for 5 components, but the Developer can't start until ALL specs are done.

### Solution

Evolve to a **pipelined parallelism** model with PM as active coordinator:

```
Architect: [Auth][API][Data][UI]
                 ↓    ↓    ↓   ↓  (specs handed off as completed)
Developer:      [Auth][API][Data][UI]
                      ↓    ↓    ↓   ↓  (implementations handed off)
Reviewer:            [Auth][API][Data][UI]
```

The PM agent becomes a **long-running coordinator** that:
- Receives completed work units from agents
- Makes routing decisions (approve, revise, implement)
- Manages concurrent work streams
- Tracks overall project progress

### Benefits

| Metric | Pipeline | Orchestrator | Improvement |
|--------|----------|--------------|-------------|
| Total time (5 components) | ~150 min | ~70 min | **53% faster** |
| Agent idle time | High | Low | **Better utilization** |
| Feedback loops | End of phase | Per component | **Faster iteration** |
| Error impact | Entire phase | Single component | **Reduced blast radius** |

---

## Architecture Comparison

### Current: Sequential Pipeline

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Sequential Pipeline                                  │
└─────────────────────────────────────────────────────────────────────────────┘

     ┌──────────┐      ┌──────────┐      ┌──────────┐      ┌──────────┐
     │    PM    │─────▶│ Architect│─────▶│Developer │─────▶│ Reviewer │
     │(analyze) │      │(all specs)│     │(all code)│      │(all code)│
     └──────────┘      └──────────┘      └──────────┘      └──────────┘
          │                  │                 │                 │
          ▼                  ▼                 ▼                 ▼
      issue-spec.md     architecture/      src/             review.md
                        ├── auth.md        ├── Auth/
                        ├── api.md         ├── Api/
                        └── data.md        └── Data/

Timeline:
PM:        [====]
Architect:       [================]
Developer:                         [================]
Reviewer:                                            [========]
                                                              ───▶ PR
```

### Proposed: Pipelined Orchestrator

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       Pipelined Orchestrator                                 │
└─────────────────────────────────────────────────────────────────────────────┘

                              ┌─────────────────────┐
                              │   PM (Coordinator)  │
                              │   - Routes work     │
                              │   - Tracks progress │
                              │   - Makes decisions │
                              └──────────┬──────────┘
                                         │
              ┌──────────────────────────┼──────────────────────────┐
              │                          │                          │
              ▼                          ▼                          ▼
     ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
     │    Architect    │      │    Developer    │      │    Reviewer     │
     │ (incremental)   │      │ (incremental)   │      │ (incremental)   │
     └────────┬────────┘      └────────┬────────┘      └────────┬────────┘
              │                        │                        │
    ┌─────────┴─────────┐    ┌─────────┴─────────┐    ┌─────────┴─────────┐
    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼    ▼
  [Auth][API][Data][UI]    [Auth][API][Data][UI]    [Auth][API][Data][UI]


Timeline (pipelined):
PM:        [====]──────────────────────────────────────────────[==]
Architect:       [Auth][API][Data][UI]
Developer:            [Auth][API][Data][UI]
Reviewer:                  [Auth][API][Data][UI]
                                                    ───▶ PR (earlier!)
```

---

## Core Concepts

### Work Unit

A **Work Unit** is the atomic unit of work that flows through the system. Smaller than an "agent task", it represents a single deliverable.

```
Work Unit Examples:
├── spec:auth          - Architecture spec for Auth component
├── spec:api           - Architecture spec for API layer
├── impl:auth          - Implementation of Auth component
├── impl:api           - Implementation of API layer
├── review:auth        - Code review of Auth implementation
└── test:auth          - Tests for Auth component
```

### Work Unit Lifecycle

```
                    ┌─────────────┐
                    │   Created   │
                    └──────┬──────┘
                           │ PM assigns to agent
                    ┌──────▼──────┐
                    │ In Progress │
                    └──────┬──────┘
                           │ Agent completes
                    ┌──────▼──────┐
              ┌─────│  Submitted  │─────┐
              │     └─────────────┘     │
              │ PM approves             │ PM requests revision
       ┌──────▼──────┐           ┌──────▼──────┐
       │   Routed    │           │  Revision   │
       │ (to next    │           │  Requested  │
       │  agent)     │           └──────┬──────┘
       └──────┬──────┘                  │
              │                         │ Agent revises
              │                  ┌──────▼──────┐
              │                  │ In Progress │ (back to loop)
              │                  └─────────────┘
       ┌──────▼──────┐
       │  Completed  │
       └─────────────┘
```

### Component

A **Component** is a logical grouping of related work units. The PM identifies components during initial analysis.

```
Component: "Authentication"
├── spec:auth      → Architect
├── impl:auth      → Developer (after spec approved)
├── review:auth    → Reviewer (after impl complete)
└── test:auth      → Tester (parallel with review)
```

---

## Data Models

### WorkUnit Entity

```csharp
public class WorkUnit
{
    public required string Id { get; set; }              // "spec:auth", "impl:api"
    public required string ProjectId { get; set; }
    public required string Component { get; set; }       // "auth", "api", "data"
    public required WorkUnitType Type { get; set; }      // Spec, Implementation, Review, Test
    public WorkUnitStatus Status { get; set; }

    public string? AssignedAgent { get; set; }           // "architect", "developer"
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? ArtifactPath { get; set; }            // Path to output file
    public string? ParentWorkUnitId { get; set; }        // spec:auth is parent of impl:auth
    public string? Notes { get; set; }                   // PM's routing notes

    public int RevisionCount { get; set; }               // Track revision loops
    public string? RevisionFeedback { get; set; }        // Why revision requested
}

public enum WorkUnitType
{
    Spec,           // Architecture/design specification
    Design,         // UI/UX design (Designer agent)
    Implementation, // Code implementation
    Review,         // Code review
    Test            // Test creation/execution
}

public enum WorkUnitStatus
{
    Pending,            // Created but not assigned
    Assigned,           // Assigned to agent, not started
    InProgress,         // Agent actively working
    Submitted,          // Agent completed, awaiting PM review
    RevisionRequested,  // PM requested changes
    Approved,           // PM approved, ready for next phase
    Routed,             // Handed to next agent
    Completed,          // Fully done (review passed, tests passed)
    Blocked,            // Cannot proceed (dependency issue)
    Failed              // Unrecoverable failure
}
```

### ProjectPlan Entity

```csharp
public class ProjectPlan
{
    public required string Id { get; set; }
    public required string ProjectId { get; set; }
    public required int IssueNumber { get; set; }

    public required IList<ComponentPlan> Components { get; set; }
    public ProjectPlanStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Computed
    public int TotalWorkUnits => Components.Sum(c => c.WorkUnits.Count);
    public int CompletedWorkUnits => Components.Sum(c =>
        c.WorkUnits.Count(w => w.Status == WorkUnitStatus.Completed));
    public double ProgressPercent => TotalWorkUnits > 0
        ? (double)CompletedWorkUnits / TotalWorkUnits * 100
        : 0;
}

public class ComponentPlan
{
    public required string Name { get; set; }           // "auth", "api"
    public required string Description { get; set; }
    public int Priority { get; set; }                   // Execution order hint
    public required IList<WorkUnit> WorkUnits { get; set; }
    public IList<string> Dependencies { get; set; } = [];  // Other component names
}
```

### Database Schema Additions

```sql
-- New tables for orchestrator model

CREATE TABLE WorkUnits (
    Id TEXT PRIMARY KEY,
    ProjectId TEXT NOT NULL,
    Component TEXT NOT NULL,
    Type TEXT NOT NULL,
    Status TEXT NOT NULL,
    AssignedAgent TEXT,
    AssignedAt TEXT,
    CompletedAt TEXT,
    ArtifactPath TEXT,
    ParentWorkUnitId TEXT,
    Notes TEXT,
    RevisionCount INTEGER DEFAULT 0,
    RevisionFeedback TEXT,
    FOREIGN KEY (ProjectId) REFERENCES ProjectStates(Id),
    FOREIGN KEY (ParentWorkUnitId) REFERENCES WorkUnits(Id)
);

CREATE TABLE ProjectPlans (
    Id TEXT PRIMARY KEY,
    ProjectId TEXT NOT NULL,
    IssueNumber INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ComponentsJson TEXT NOT NULL,  -- JSON array of ComponentPlan
    CreatedAt TEXT NOT NULL,
    CompletedAt TEXT,
    FOREIGN KEY (ProjectId) REFERENCES ProjectStates(Id)
);

CREATE INDEX IX_WorkUnits_ProjectId ON WorkUnits(ProjectId);
CREATE INDEX IX_WorkUnits_Status ON WorkUnits(Status);
CREATE INDEX IX_WorkUnits_Component ON WorkUnits(Component);
```

---

## PM Coordinator Behavior

### Coordination Loop

The PM agent runs a continuous coordination loop:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        PM Coordination Loop                                  │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌──────────────────┐
    │ 1. Initial       │  Analyze issue, identify components,
    │    Analysis      │  create ProjectPlan with WorkUnits
    └────────┬─────────┘
             │
    ┌────────▼─────────┐
    │ 2. Spawn Workers │  Start Architect (and Designer if needed)
    │                  │  Assign first batch of spec WorkUnits
    └────────┬─────────┘
             │
             ▼
    ┌─────────────────────────────────────────────────────────────┐
    │                                                             │
    │  ┌──────────────────┐                                       │
    │  │ 3. Poll for      │  Check apmas_get_context for:         │
    │  │    Updates       │  - Submitted work units               │
    │  └────────┬─────────┘  - Agent messages                     │
    │           │            - Status changes                     │
    │  ┌────────▼─────────┐                                       │
    │  │ 4. Review        │  For each submitted work unit:        │
    │  │    Submissions   │  - Read artifact                      │
    │  └────────┬─────────┘  - Assess quality                     │
    │           │                                                 │
    │  ┌────────▼─────────┐                                       │
    │  │ 5. Make Routing  │  Decision tree:                       │
    │  │    Decisions     │  - Major issues → Request revision    │
    │  └────────┬─────────┘  - Minor issues → Note for developer  │
    │           │            - Good → Route to next agent         │
    │  ┌────────▼─────────┐                                       │
    │  │ 6. Assign Next   │  - Route approved specs to Developer  │
    │  │    Work          │  - Route impl to Reviewer/Tester      │
    │  └────────┬─────────┘  - Start new specs if capacity        │
    │           │                                                 │
    │  ┌────────▼─────────┐                                       │
    │  │ 7. Update Status │  - Checkpoint progress                │
    │  │                  │  - Report to external webhook         │
    │  └────────┬─────────┘                                       │
    │           │                                                 │
    │           │ (loop until all WorkUnits completed)            │
    │           └─────────────────────────────────────────────────┘
    │
    ┌────────▼─────────┐
    │ 8. Finalization  │  All WorkUnits done:
    │                  │  - Create PR
    │                  │  - Report completion
    │                  │  - Call apmas_complete
    └──────────────────┘
```

### PM Prompt: ProjectManagerCoordinatorPrompt.cs

```csharp
public class ProjectManagerCoordinatorPrompt : BaseAgentPrompt
{
    public override string Role => "Project Manager";
    public override string SubagentType => "general-purpose";

    protected override string GetRoleDescription() => """
        You are the **Project Manager and Active Coordinator** for this project.
        Unlike other agents who complete a task and exit, you **stay alive throughout
        the entire project**, coordinating work between agents.

        Your responsibilities:
        1. **Analyze** the GitHub issue and break it into components
        2. **Plan** the work units needed for each component
        3. **Coordinate** agents by assigning and routing work units
        4. **Review** submitted work and make routing decisions
        5. **Track** overall progress and handle blockers
        6. **Finalize** the project with PR creation
        """;

    protected override string GetTaskDescription() => """
        ## Phase 1: Initial Analysis (Do Once)

        1. **Fetch the GitHub issue:**
           ```bash
           gh issue view {ISSUE_NUMBER} --json title,body,labels,comments
           ```

        2. **Explore the codebase** to understand architecture and patterns

        3. **Identify components** - logical units of work:
           - Each component should be independently implementable
           - Components may have dependencies on each other
           - Aim for 2-6 components for medium issues

        4. **Create the project plan** by calling:
           ```
           apmas_create_plan(
             issueNumber: {N},
             components: [
               {
                 name: "auth",
                 description: "Authentication service and middleware",
                 priority: 1,
                 dependencies: []
               },
               {
                 name: "api",
                 description: "REST API endpoints",
                 priority: 2,
                 dependencies: ["auth"]
               }
             ]
           )
           ```

        ## Phase 2: Spawn Workers (Do Once)

        5. **Start the Architect** to begin spec work:
           ```
           apmas_send_message(
             to: "architect",
             type: "assignment",
             content: "Begin specs for components: auth, api, data. Work incrementally."
           )
           ```

        6. **Start Designer** (only if UI work identified):
           ```
           apmas_send_message(to: "designer", type: "assignment", content: "...")
           ```

        ## Phase 3: Coordination Loop (Repeat)

        Now enter your coordination loop. Every 2-3 minutes:

        7. **Check for updates:**
           ```
           apmas_get_context(include: ["messages", "workUnits"])
           ```

        8. **For each submitted work unit**, review the artifact:
           - Read the file at `artifactPath`
           - Assess: Does it meet requirements? Any major issues?

        9. **Make routing decision:**

           ```
           IF work unit has MAJOR issues (wrong approach, missing requirements):
               apmas_route_work(
                 workUnitId: "spec:auth",
                 decision: "revision",
                 feedback: "Missing error handling requirements. See issue comment #3."
               )

           ELSE IF work unit has MINOR issues (style, small gaps):
               apmas_route_work(
                 workUnitId: "spec:auth",
                 decision: "approve",
                 notes: "Good spec. Developer: note the auth should use JWT, not sessions."
               )
               // This automatically creates impl:auth and assigns to Developer

           ELSE (work unit is good):
               apmas_route_work(
                 workUnitId: "spec:auth",
                 decision: "approve"
               )
           ```

        10. **Check agent capacity** - if Developer finished impl:auth, they can start impl:api

        11. **Checkpoint your state:**
            ```
            apmas_checkpoint(
              summary: "3/8 work units complete. Auth implemented, API in progress.",
              completedItems: ["spec:auth", "spec:api", "impl:auth"],
              pendingItems: ["impl:api", "review:auth", ...]
            )
            ```

        12. **Heartbeat:**
            ```
            apmas_heartbeat(status: "coordinating", progress: "3/8 units complete")
            ```

        13. **Repeat** until all work units are Completed

        ## Phase 4: Finalization (Do Once)

        14. **All work complete** - create the PR:
            ```bash
            gh pr create --title "Fix #{ISSUE_NUMBER}: {title}" --body "..."
            ```

        15. **Report completion:**
            ```
            apmas_complete(
              summary: "Implemented {N} components across {M} files",
              artifacts: ["src/...", "tests/..."],
              pullRequestUrl: "https://github.com/..."
            )
            ```
        """;

    protected override string GetDeliverables() => """
        1. **Project Plan** - Created via `apmas_create_plan`
        2. **Coordination** - All work units routed and completed
        3. **Pull Request** - Created with proper description linking to issue
        4. **Completion Report** - Summary of all work done
        """;

    protected override string GetDependencies() => """
        **No agent dependencies** - you are the coordinator.

        **You manage these agents:**
        - Architect: Creates component specifications
        - Designer: Creates UI/UX specifications (if needed)
        - Developer: Implements components
        - Reviewer: Reviews implementations
        - Tester: Creates and runs tests
        """;
}
```

### Routing Decision Tree

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     PM Routing Decision Tree                                 │
└─────────────────────────────────────────────────────────────────────────────┘

Submitted WorkUnit
       │
       ▼
┌──────────────────┐
│ Read Artifact    │
│ (spec/code/etc)  │
└────────┬─────────┘
         │
         ▼
┌──────────────────────────────────────┐
│ Does it meet the requirements?       │
│ - Addresses the issue?               │
│ - Follows patterns?                  │
│ - No major gaps?                     │
└────────┬─────────────────────────────┘
         │
    ┌────┴────┐
    │         │
   NO        YES
    │         │
    ▼         ▼
┌────────┐  ┌─────────────────────────────┐
│REVISION│  │ Any minor issues to note?   │
│        │  └────────┬────────────────────┘
│Feedback│           │
│to agent│      ┌────┴────┐
└────────┘      │         │
               YES        NO
                │         │
                ▼         ▼
          ┌─────────┐  ┌─────────┐
          │ APPROVE │  │ APPROVE │
          │ w/notes │  │ (clean) │
          └─────────┘  └─────────┘
                │         │
                └────┬────┘
                     │
                     ▼
         ┌───────────────────────┐
         │ Create next WorkUnit  │
         │ and assign to next    │
         │ agent in pipeline     │
         └───────────────────────┘

         spec:auth (approved) → impl:auth (assigned to Developer)
         impl:auth (approved) → review:auth (assigned to Reviewer)
                              → test:auth (assigned to Tester)
```

---

## Agent Modifications

### Architect: Incremental Specs

```csharp
protected override string GetTaskDescription() => """
    Work through components **incrementally**. For each component:

    1. **Design** the component architecture
    2. **Write spec** to `docs/specs/components/{component}.md`
    3. **Submit** via:
       ```
       apmas_submit_work(
         workUnitId: "spec:{component}",
         artifactPath: "docs/specs/components/{component}.md",
         summary: "Spec for {component} complete"
       )
       ```
    4. **Immediately start** the next component (don't wait for approval)

    ## Handling Revisions

    The PM may request revisions. Check for messages:
    ```
    apmas_get_context(include: ["messages"])
    ```

    If you receive a revision request:
    1. Read the feedback
    2. Update the spec
    3. Re-submit with incremented revision

    ## Spec Format

    Each component spec should include:
    - Purpose and responsibilities
    - Public interfaces/APIs
    - Dependencies on other components
    - Key implementation notes
    - File/folder structure
    """;
```

### Developer: Work Unit Based

```csharp
protected override string GetTaskDescription() => """
    You receive **work units** from the PM. For each assigned work unit:

    1. **Get assignment** via messages or work unit status
    2. **Read the spec** at the artifact path
    3. **Implement** following the spec and existing patterns
    4. **Submit** when complete:
       ```
       apmas_submit_work(
         workUnitId: "impl:{component}",
         artifactPath: "src/{Component}/",
         summary: "Implementation complete"
       )
       ```
    5. **Check for next assignment** - PM may have queued more work

    ## Handling Multiple Components

    You may be implementing component A while Architect is still speccing B.
    This is normal. Work on what's assigned, PM will route new work as ready.

    ## Handling Revision Requests

    If PM or Reviewer requests changes:
    1. Read the feedback in the message
    2. Make the requested changes
    3. Re-submit the work unit
    """;
```

### Reviewer: Incremental Reviews

```csharp
protected override string GetTaskDescription() => """
    You review implementations **as they complete**, not all at once.

    For each assigned review work unit:

    1. **Get the implementation** path from work unit
    2. **Read the spec** (parent work unit's artifact)
    3. **Review** the implementation:
       - Does it match the spec?
       - Code quality and patterns?
       - Security considerations?
       - Edge cases handled?

    4. **Submit review:**
       ```
       apmas_submit_work(
         workUnitId: "review:{component}",
         artifactPath: "docs/reviews/{component}.md",
         summary: "Review complete - {approved|changes_requested}",
         metadata: { "verdict": "approved" }  // or "changes_requested"
       )
       ```

    ## Review Verdicts

    - **approved**: Implementation is good, can proceed
    - **changes_requested**: Developer needs to revise (PM will route back)
    """;
```

---

## New MCP Tools

### apmas_create_plan

PM creates the project plan with components and work units.

```json
{
  "name": "apmas_create_plan",
  "description": "Create a project plan with components (PM only)",
  "inputSchema": {
    "type": "object",
    "properties": {
      "issueNumber": {
        "type": "integer",
        "description": "GitHub issue number"
      },
      "components": {
        "type": "array",
        "items": {
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "description": { "type": "string" },
            "priority": { "type": "integer" },
            "dependencies": {
              "type": "array",
              "items": { "type": "string" }
            },
            "skipDesign": { "type": "boolean" },
            "skipReview": { "type": "boolean" }
          },
          "required": ["name", "description"]
        }
      }
    },
    "required": ["issueNumber", "components"]
  }
}
```

**Implementation:**
```csharp
public async Task<ToolResult> ExecuteAsync(CreatePlanInput input)
{
    var plan = new ProjectPlan
    {
        Id = Guid.NewGuid().ToString(),
        ProjectId = _currentProjectId,
        IssueNumber = input.IssueNumber,
        Components = input.Components.Select(c => new ComponentPlan
        {
            Name = c.Name,
            Description = c.Description,
            Priority = c.Priority,
            Dependencies = c.Dependencies ?? [],
            WorkUnits = GenerateWorkUnits(c)
        }).ToList()
    };

    await _stateStore.SaveProjectPlanAsync(plan);
    return ToolResult.Success($"Plan created with {plan.TotalWorkUnits} work units");
}

private List<WorkUnit> GenerateWorkUnits(ComponentInput c)
{
    var units = new List<WorkUnit>
    {
        new() { Id = $"spec:{c.Name}", Type = WorkUnitType.Spec, Status = WorkUnitStatus.Pending }
    };

    if (!c.SkipDesign)
        units.Add(new() { Id = $"design:{c.Name}", Type = WorkUnitType.Design, ... });

    units.Add(new() { Id = $"impl:{c.Name}", Type = WorkUnitType.Implementation, ... });

    if (!c.SkipReview)
        units.Add(new() { Id = $"review:{c.Name}", Type = WorkUnitType.Review, ... });

    units.Add(new() { Id = $"test:{c.Name}", Type = WorkUnitType.Test, ... });

    return units;
}
```

---

### apmas_submit_work

Agents submit completed work units.

```json
{
  "name": "apmas_submit_work",
  "description": "Submit a completed work unit for PM review",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": { "type": "string" },
      "workUnitId": { "type": "string" },
      "artifactPath": { "type": "string" },
      "summary": { "type": "string" },
      "metadata": { "type": "object" }
    },
    "required": ["agentRole", "workUnitId", "artifactPath", "summary"]
  }
}
```

---

### apmas_route_work

PM routes work units after review.

```json
{
  "name": "apmas_route_work",
  "description": "Route a submitted work unit (PM only)",
  "inputSchema": {
    "type": "object",
    "properties": {
      "workUnitId": { "type": "string" },
      "decision": {
        "type": "string",
        "enum": ["approve", "revision"]
      },
      "feedback": {
        "type": "string",
        "description": "Required if decision is 'revision'"
      },
      "notes": {
        "type": "string",
        "description": "Optional notes for next agent"
      }
    },
    "required": ["workUnitId", "decision"]
  }
}
```

**Implementation:**
```csharp
public async Task<ToolResult> ExecuteAsync(RouteWorkInput input)
{
    var workUnit = await _stateStore.GetWorkUnitAsync(input.WorkUnitId);

    if (input.Decision == "revision")
    {
        workUnit.Status = WorkUnitStatus.RevisionRequested;
        workUnit.RevisionFeedback = input.Feedback;
        workUnit.RevisionCount++;

        await _messageBus.PublishAsync(new AgentMessage
        {
            From = "project-manager",
            To = workUnit.AssignedAgent!,
            Type = MessageType.ChangesRequested,
            Content = input.Feedback
        });
    }
    else // approve
    {
        workUnit.Status = WorkUnitStatus.Approved;
        workUnit.Notes = input.Notes;

        // Create and assign next work unit in pipeline
        var nextUnit = await CreateNextWorkUnitAsync(workUnit);
        if (nextUnit != null)
        {
            await AssignWorkUnitAsync(nextUnit);
        }
    }

    await _stateStore.SaveWorkUnitAsync(workUnit);
    return ToolResult.Success($"Work unit {input.WorkUnitId} routed: {input.Decision}");
}
```

---

### apmas_get_work_status

Get current status of all work units.

```json
{
  "name": "apmas_get_work_status",
  "description": "Get status of all work units in the project",
  "inputSchema": {
    "type": "object",
    "properties": {
      "filter": {
        "type": "string",
        "enum": ["all", "pending", "in_progress", "submitted", "completed"]
      },
      "component": {
        "type": "string",
        "description": "Filter by component name"
      }
    }
  }
}
```

---

## Supervisor Changes

### Orchestrator Mode

```csharp
public class SupervisorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait for HTTP server
        await _serverReadySignal.WaitForServerAsync(ct);

        // Initialize project
        await InitializeProjectAsync();

        // Check if orchestrator mode (PM agent configured)
        if (_options.UseOrchestratorMode)
        {
            await ExecuteOrchestratorModeAsync(ct);
        }
        else
        {
            await ExecutePipelineModeAsync(ct);  // Existing behavior
        }
    }

    private async Task ExecuteOrchestratorModeAsync(CancellationToken ct)
    {
        // 1. Spawn PM agent
        var pmResult = await _spawner.SpawnAgentAsync("project-manager", "general-purpose");

        // 2. PM runs its coordination loop (it's long-running)
        // 3. Supervisor monitors PM health and handles PM timeout/restart
        // 4. Supervisor spawns other agents as PM requests via work unit assignments

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_options.PollingInterval, ct);

            // Check PM health
            if (!_heartbeatMonitor.IsAgentHealthy("project-manager"))
            {
                await HandlePmTimeoutAsync();
            }

            // Check for agent spawn requests (from work unit assignments)
            await SpawnRequestedAgentsAsync();

            // Check for project completion
            var plan = await _stateStore.GetProjectPlanAsync();
            if (plan?.Status == ProjectPlanStatus.Completed)
            {
                _logger.LogInformation("Project completed!");
                break;
            }
        }
    }

    private async Task SpawnRequestedAgentsAsync()
    {
        // Check work units that are Assigned but agent not running
        var assignedUnits = await _stateStore.GetWorkUnitsAsync(WorkUnitStatus.Assigned);

        foreach (var unit in assignedUnits)
        {
            var agentRole = GetAgentRoleForWorkUnit(unit);
            var process = await _spawner.GetAgentProcessAsync(agentRole);

            if (process == null || process.Status != AgentProcessStatus.Running)
            {
                await _spawner.SpawnAgentAsync(agentRole, GetSubagentType(agentRole));
            }
        }
    }
}
```

---

## Implementation Phases

### Phase 1: Work Unit Foundation
**Duration:** 3-4 days
**Goal:** Establish work unit model and basic tracking

- [ ] Create WorkUnit and ProjectPlan entities
- [ ] Add database migrations
- [ ] Implement IWorkUnitStore interface
- [ ] Create apmas_create_plan tool
- [ ] Create apmas_submit_work tool
- [ ] Create apmas_get_work_status tool
- [ ] Unit tests for work unit operations

**Deliverable:** Work units can be created, tracked, and queried

---

### Phase 2: PM Coordinator Agent
**Duration:** 4-5 days
**Goal:** PM agent that coordinates via work units

- [ ] Create ProjectManagerCoordinatorPrompt
- [ ] Implement apmas_route_work tool
- [ ] Update PromptFactory for new prompt type
- [ ] PM coordination loop behavior
- [ ] Integration test: PM creates plan and routes specs

**Deliverable:** PM can analyze issue, create plan, and route work

---

### Phase 3: Agent Incremental Mode
**Duration:** 3-4 days
**Goal:** Agents work on individual work units

- [ ] Update ArchitectPrompt for incremental specs
- [ ] Update DeveloperPrompt for work unit assignments
- [ ] Update ReviewerPrompt for incremental reviews
- [ ] Update TesterPrompt for incremental tests
- [ ] Agents check for and respond to revision requests

**Deliverable:** All agents work in incremental, work-unit mode

---

### Phase 4: Orchestrator Supervisor
**Duration:** 3-4 days
**Goal:** Supervisor supports orchestrator mode

- [ ] Add OrchestratorOptions configuration
- [ ] Implement ExecuteOrchestratorModeAsync
- [ ] Agent spawn-on-demand based on work unit assignments
- [ ] PM health monitoring and recovery
- [ ] Project completion detection

**Deliverable:** Full orchestrator mode working end-to-end

---

### Phase 5: Polish & Optimization
**Duration:** 2-3 days
**Goal:** Refine the orchestrator experience

- [ ] Concurrent agent execution (multiple developers)
- [ ] Work unit dependency validation
- [ ] Progress reporting improvements
- [ ] External webhook notifications per work unit
- [ ] Dashboard events for work unit status changes

**Deliverable:** Production-ready orchestrator mode

---

## Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| PM agent context exhaustion | Medium | High | Aggressive checkpointing, work unit state in DB not context |
| Infinite revision loops | Low | Medium | Max revision count (3), escalate to human |
| Agent coordination deadlock | Low | High | Timeout detection, PM can reassign work |
| Complexity increase | High | Medium | Good logging, clear state visualization |
| Backward compatibility | Medium | Low | Feature flag for orchestrator mode |

---

## Appendix: Example Execution Trace

```
[00:00] PM: Analyzing issue #25
[00:02] PM: Created plan with 3 components: auth, api, data
[00:02] PM: Spawning Architect
[00:03] Architect: Starting spec for auth
[00:08] Architect: Submitted spec:auth
[00:08] PM: Reviewing spec:auth... APPROVED
[00:08] PM: Created impl:auth, assigning to Developer
[00:08] PM: Spawning Developer
[00:09] Architect: Starting spec for api
[00:09] Developer: Starting impl:auth
[00:15] Architect: Submitted spec:api
[00:15] PM: Reviewing spec:api... REVISION (missing error handling)
[00:16] Architect: Revising spec:api
[00:18] Developer: Submitted impl:auth
[00:18] PM: Reviewing impl:auth... APPROVED
[00:18] PM: Created review:auth and test:auth
[00:18] PM: Spawning Reviewer and Tester
[00:19] Architect: Submitted spec:api (revision 1)
[00:19] PM: Reviewing spec:api... APPROVED
[00:19] PM: Created impl:api, assigning to Developer
[00:20] Reviewer: Starting review:auth
[00:20] Tester: Starting test:auth
[00:20] Developer: Starting impl:api
[00:25] Reviewer: Submitted review:auth - APPROVED
[00:26] Tester: Submitted test:auth - PASSED
[00:26] PM: auth component COMPLETE
... (continues for api, data)
[00:45] PM: All components complete
[00:45] PM: Creating PR...
[00:46] PM: PR #42 created. Project complete!
```

---

*Draft Version: 0.1*
*Last Updated: 2026-01-22*