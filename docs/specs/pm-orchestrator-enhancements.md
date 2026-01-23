# PM Orchestrator Enhancement Specification

**Status:** Draft - Pending Architect Review
**Date:** 2026-01-22
**Enhances:** Issue #89 (PM Orchestrator Architecture) and sub-issues #63-93
**Reference:** [pm-orchestrator-design.md](../planning/pm-orchestrator-design.md)

---

## Executive Summary

This document describes enhancements to the planned PM Orchestrator architecture. The existing plan (#89) establishes work units, pipelined parallelism, and PM coordination. This enhancement evolves PM from a **long-running Claude agent** to an **autonomous orchestrator with on-demand intelligence**.

**Key Change:** PM becomes a C# service (not a Claude agent) that uses ephemeral Claude calls to "think" when decisions require intelligence.

---

## Current Plan vs Enhanced Vision

| Aspect | Current Plan (#89) | Enhanced Vision |
|--------|-------------------|-----------------|
| **PM Implementation** | Long-running Claude agent with coordination loop | C# service with ephemeral Claude "think" calls |
| **Input Handling** | GitHub issue number provided | Any input: project brief, feature request, bug description |
| **Workflow Selection** | Implicit (always full pipeline) | PM classifies input and selects appropriate workflow |
| **Agent Routing** | PM routes via `apmas_route_work` | PM decides based on scope + workflow type |
| **Communication** | Agents submit, PM reviews | Bidirectional: agents report to PM, PM instructs agents |
| **Context Management** | PM maintains context in Claude session | State in SQL, fresh context per "think" call |

---

## Enhancement 1: PM as C# Service with "Brain"

### Problem with Current Plan

The current plan has PM as a Claude agent running a coordination loop:
```
PM Agent (Claude) → polls for updates → reviews artifacts → routes work
```

This suffers from:
- Context exhaustion on complex projects
- State loss on checkpoint/restart
- Expensive (full Claude session for coordination)

### Enhanced Design

PM is a C# BackgroundService that calls Claude only when intelligence is needed:

```
PM Service (C#) → event loop → deterministic routing
                           ↓
                   needs decision?
                           ↓
                   ephemeral Claude "think" call
```

```csharp
public interface IClaudeBrain
{
    /// <summary>
    /// Make an ephemeral Claude call for a specific decision.
    /// Fresh context every call - no accumulation.
    /// </summary>
    Task<T> ThinkAsync<T>(string prompt, string? modelHint = null);
}

public class PmOrchestrator : BackgroundService
{
    private readonly IClaudeBrain _brain;
    private readonly IWorkUnitStore _store;  // SQL state
    private readonly IAgentSpawner _spawner;

    // State lives in SQL, not Claude context
    // "Think" calls are stateless and ephemeral
}
```

### When PM "Thinks"

| Situation | Think Call |
|-----------|------------|
| Classify input | "Is this a new project, feature request, or bug fix?" |
| Assess scope | "Given this issue and codebase, is this TRIVIAL/SMALL/MEDIUM/LARGE?" |
| Review artifact | "Does this spec meet the requirements? APPROVE or REVISION?" |
| Handle failure | "Agent failed with X. Retry, reduce scope, or escalate?" |
| Route ambiguity | "Should this go to Developer or Designer?" |

---

## Enhancement 2: Input Classification & Workflow Selection

### Problem with Current Plan

Current plan assumes input is always a GitHub issue number with a predefined workflow.

### Enhanced Design

PM accepts any input and classifies it:

```csharp
public enum WorkflowType
{
    NewProject,      // PROJECT-BRIEF.md → full pipeline
    FeatureRequest,  // Feature/issue → scoped pipeline
    QuickFix         // Bug/typo → minimal pipeline
}

public enum IssueScope
{
    Trivial,  // Single-line fix → dotnet-fixer (haiku)
    Small,    // Single-file → dotnet-fixer (haiku)
    Medium,   // Multi-file → dotnet-specialist (sonnet)
    Large     // Architectural → full planning
}
```

**Workflow Decision Tree:**

```
Input received
    │
    ▼
"Think": Classify input type
    │
    ├─→ NEW_PROJECT (has PROJECT-BRIEF.md)
    │       → Full workflow: Init → Architect → Designer → Developer → Review → Test
    │
    ├─→ FEATURE_REQUEST (existing codebase)
    │       │
    │       ▼
    │   "Think": Assess scope
    │       │
    │       ├─→ LARGE: Architect planning → work units → full review
    │       ├─→ MEDIUM: Light planning → Developer → Review
    │       └─→ SMALL: Direct to Developer → Quick review
    │
    └─→ BUG_FIX (quick fix)
            → dotnet-fixer → PR
```

### Context Gathering

Before making decisions, PM gathers project context:

```csharp
public class ProjectContext
{
    public string? ClaudeMd { get; set; }           // Raw CLAUDE.md if exists
    public string Architecture { get; set; }        // "Three-layer: Core → Infra → App"
    public Dictionary<string, string> KeyPaths { get; set; }  // {"services": "src/X/Services/"}
    public Dictionary<string, string> Patterns { get; set; }  // {"di": "IServiceCollection extensions"}
    public List<string> BuildCommands { get; set; } // ["dotnet build", "dotnet test"]
}
```

If CLAUDE.md exists, parse it. Otherwise, "think" to analyze codebase structure.

---

## Enhancement 3: Bidirectional Agent Communication

### Problem with Current Plan

Current plan has agents submit work, PM reviews. But agents can't report nuanced feedback or request decisions.

### Enhanced Design

Hub-and-spoke model where all communication flows through PM:

```
        ┌─────────┐
        │   PM    │ ← All decisions made here
        └────┬────┘
             │
    ┌────────┼────────┐
    ▼        ▼        ▼
 Agent A  Agent B  Agent C
    │        │        │
    └────────┴────────┘
         │
    Messages TO PM
    (not to each other)
```

### Agent → PM Messages

```csharp
public enum AgentMessageType
{
    // Completion
    WorkCompleted,          // "I finished, here's what I did"

    // Review outcomes
    ReviewApproved,         // "Code looks good"
    ReviewChangesRequested, // "Found issues, here's feedback"

    // Test outcomes
    TestsPassed,            // "All tests pass"
    TestsFailed,            // "Some tests failed, here's details"

    // Blockers
    WorkBlocked,            // "I'm stuck on X"
    NeedsClarification,     // "Requirements unclear"
    RequestHumanInput,      // "Need human decision"
}

public class AgentReport
{
    public string AgentRole { get; set; }
    public AgentMessageType Type { get; set; }
    public string Component { get; set; }
    public object Payload { get; set; }  // Type-specific data
}
```

### PM → Agent Instructions

PM can send instructions to running agents:

```csharp
public enum PmInstructionType
{
    Continue,       // "Keep going"
    Pause,          // "Wait for further instructions"
    Revise,         // "Fix these issues" + feedback
    Abort,          // "Stop, we're changing approach"
    ExpandScope,    // "Also handle X while you're there"
    ReduceScope,    // "Skip X, just do Y"
}
```

### New MCP Tools

```csharp
// Agent reports to PM (replaces generic apmas_report_status for substantive updates)
"apmas_report_to_pm" → {
    agentRole: string,
    messageType: AgentMessageType,
    component: string,
    payload: object  // ReviewFeedback, TestResults, BlockerInfo, etc.
}

// PM sends instruction to agent
"apmas_pm_instruct" → {
    toAgent: string,
    instructionType: PmInstructionType,
    instructions: string,
    context: object?
}
```

---

## Enhancement 4: Structured Context Passing

### Problem with Current Plan

Work units have `notes` field but no structured context. Agents receive vague instructions.

### Enhanced Design

Rich context model passed to agents:

```csharp
public class StructuredContext
{
    // Task specifics
    public string TaskSummary { get; set; }           // 1-2 sentences
    public List<string> Requirements { get; set; }    // Bullet points

    // Project context (from CLAUDE.md or gathered)
    public string Architecture { get; set; }          // Layer structure
    public Dictionary<string, string> KeyPaths { get; set; }
    public Dictionary<string, string> Patterns { get; set; }

    // Specific guidance
    public List<string> FilesToModify { get; set; }   // Exact paths
    public List<string> FilesToReadFirst { get; set; } // Interfaces, examples
    public string? PatternReference { get; set; }     // "Follow src/Services/X.cs"
    public List<string> Constraints { get; set; }     // "Use IMemoryCache", etc.

    // From previous agents (if applicable)
    public string? SpecArtifactPath { get; set; }     // Architect's output
    public string? DesignArtifactPath { get; set; }   // Designer's output
    public ReviewFeedback? RevisionFeedback { get; set; } // If fixing issues
}
```

Agent prompts are built FROM this context, not written generically.

---

## Enhancement 5: Scope-Aware Agent Selection

### Problem with Current Plan

Fixed agent roster with fixed subagent types. A typo fix uses same agents as new subsystem.

### Enhanced Design

PM selects agent AND model based on scope:

```csharp
public class AgentAssignment
{
    public string AgentRole { get; set; }       // "developer"
    public string SubagentType { get; set; }    // "dotnet-fixer" vs "dotnet-specialist"
    public string ModelHint { get; set; }       // "haiku" vs "sonnet"
    public StructuredContext Context { get; set; }
}

// PM logic
AgentAssignment SelectAgent(IssueScope scope, WorkUnitType type)
{
    return (scope, type) switch
    {
        (Trivial or Small, Implementation) => new AgentAssignment
        {
            AgentRole = "developer",
            SubagentType = "dotnet-fixer",
            ModelHint = "haiku"
        },
        (Medium or Large, Implementation) => new AgentAssignment
        {
            AgentRole = "developer",
            SubagentType = "dotnet-specialist",
            ModelHint = "sonnet"
        },
        // ... etc
    };
}
```

---

## Enhancement 6: Review Loop Protocol

### Problem with Current Plan

`apmas_route_work` has approve/revision but no iteration limits or structured feedback format.

### Enhanced Design

Formalized review protocol:

```csharp
public class ReviewFeedback
{
    public ReviewVerdict Verdict { get; set; }  // Approved, ChangesRequested
    public List<ReviewIssue> CriticalIssues { get; set; }  // Must fix
    public List<ReviewIssue> MajorIssues { get; set; }     // Should fix
    public List<ReviewIssue> MinorIssues { get; set; }     // Optional (not passed to fixer)
    public string Summary { get; set; }
}

public class ReviewIssue
{
    public string Description { get; set; }
    public string FileLocation { get; set; }  // "src/X.cs:42"
    public string HowToFix { get; set; }
}

public class OrchestratorOptions
{
    public int MaxReviewIterations { get; set; } = 2;
    public bool SkipMinorIssuesInFeedback { get; set; } = true;
    public bool RequireTestsPass { get; set; } = true;
}
```

PM enforces iteration limits:
1. First review fails → send to developer with feedback
2. Second review fails → send to developer with feedback
3. Third review fails → proceed to PR with issues noted, or escalate

---

## Impact on Existing GitHub Issues

### Issues to Modify

| Issue | Current | Change |
|-------|---------|--------|
| #70 ProjectManagerCoordinatorPrompt | Long-running agent prompt | Becomes "analysis prompt" for initial classification only |
| #80 ExecuteOrchestratorModeAsync | Spawns PM agent | Becomes the PM event loop with "think" calls |
| #71 apmas_route_work | PM tool for routing | Keep, but PM Service calls it internally |
| #72 Update PromptFactory | For PM prompts | For structured context → prompt generation |

### New Issues Needed

| New Issue | Description | Phase |
|-----------|-------------|-------|
| Create IClaudeBrain interface | Ephemeral Claude call abstraction | 1 |
| Implement ClaudeApiBrain | Direct API implementation | 1 |
| Input classification logic | Classify PROJECT_BRIEF vs FEATURE vs BUG | 2 |
| Context gathering service | Extract/build ProjectContext | 2 |
| Scope assessment logic | TRIVIAL/SMALL/MEDIUM/LARGE classification | 2 |
| Workflow selection | Choose pipeline based on classification | 2 |
| apmas_report_to_pm tool | Agent → PM structured reporting | 3 |
| apmas_pm_instruct tool | PM → Agent instructions | 3 |
| PM event loop | Message processing with "think" decisions | 3 |
| StructuredContext model | Rich context for agent prompts | 3 |
| Scope-aware agent selection | Agent + model based on scope | 4 |
| Review iteration tracking | Enforce max iterations | 4 |

### Issues That Remain Unchanged

- #63-69: WorkUnit foundation (still needed)
- #74-78: Agent prompt updates for incremental work (still needed)
- #79: OrchestratorOptions (expand with new options)
- #81-88: Supervisor/monitoring/notifications (still needed)
- #90-93: Designer/Prototyper/UICritic updates (still needed)

---

## Implementation Phases (Revised)

### Phase 1: Brain Infrastructure
- IClaudeBrain interface
- ClaudeApiBrain implementation (direct API)
- Basic "think" call with structured output

### Phase 2: Input Handling & Context
- Input classification (project/feature/bug)
- ProjectContext gathering (parse CLAUDE.md or explore)
- Scope assessment
- Workflow selection logic

### Phase 3: Bidirectional Communication
- apmas_report_to_pm tool
- apmas_pm_instruct tool
- PM event loop (message queue processing)
- StructuredContext model and prompt generation

### Phase 4: Intelligent Routing
- Scope-aware agent selection
- Model hints (haiku vs sonnet)
- Review iteration enforcement
- Dynamic workflow execution

### Phase 5: Polish (from original plan)
- Concurrent agent execution
- Progress reporting
- Webhooks/dashboard events

---

## Example: End-to-End Flow

**User Input:** "Add dark mode toggle to settings page"

```
1. PM receives input

2. PM "thinks": Classify
   → FEATURE_REQUEST (existing codebase, specific feature)

3. PM gathers context
   → Reads CLAUDE.md, extracts architecture/paths/patterns

4. PM "thinks": Assess scope
   → SMALL (single component, UI change, clear requirements)

5. PM selects workflow
   → Skip Architect (not needed for small scope)
   → Developer → Reviewer → PR

6. PM spawns Developer (dotnet-fixer, haiku) with StructuredContext:
   - Task: "Add dark mode toggle to settings"
   - Files: ["Pages/Settings.razor", "Shared/ThemeProvider.razor"]
   - Pattern: "Follow existing toggle in Pages/Preferences.razor"

7. Developer reports: WorkCompleted
   - Files modified: [...]
   - Summary: "Added toggle with localStorage persistence"

8. PM spawns Reviewer with context

9. Reviewer reports: ReviewApproved
   - Minor issues: ["Consider adding transition animation"]

10. PM "thinks": Approved, tests needed?
    → Small UI change, manual test sufficient

11. PM creates PR
    → Done
```

---

## Questions for Architect Review

1. Should IClaudeBrain support tool use, or just text in/structured out?
2. How to handle PM restart mid-execution? (SQL state should be sufficient)
3. Should agents be able to request specific "think" calls from PM?
4. Timeout handling for "think" calls?
5. Cost tracking for "think" calls?

---

*This document enhances the existing #89 plan. The architectural foundation (work units, pipelined execution, MCP tools) remains valid. The primary change is PM implementation: C# service with ephemeral intelligence instead of long-running Claude agent.*
