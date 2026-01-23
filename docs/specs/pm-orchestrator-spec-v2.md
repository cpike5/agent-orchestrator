# PM Orchestrator Specification v2.0

**Status:** Approved for Implementation
**Date:** 2026-01-22
**Supersedes:** [pm-orchestrator-design.md](../planning/pm-orchestrator-design.md)
**Enhancement Spec:** [pm-orchestrator-enhancements.md](pm-orchestrator-enhancements.md)
**Parent Issue:** #89

---

## Executive Summary

This specification defines the PM Orchestrator architecture with enhancements that evolve the PM from a **long-running Claude agent** to an **autonomous C# service with ephemeral intelligence**.

**Key Innovation:** PM becomes a C# BackgroundService that uses ephemeral Claude API calls ("think" operations) only when intelligence is needed for decisions. This eliminates context exhaustion, reduces costs, and provides deterministic routing with AI-powered decision-making when necessary.

---

## Architecture Overview

### High-Level Design

```
┌───────────────────────────────────────────────────────────────┐
│                    PM Orchestrator Service                     │
│                      (C# BackgroundService)                    │
│  ┌──────────────────────────────────────────────────────────┐ │
│  │  Event Loop (C#)                                         │ │
│  │  - Poll for agent messages                               │ │
│  │  - Process work unit submissions                         │ │
│  │  - Handle agent failures                                 │ │
│  │  - Deterministic routing when rules are clear            │ │
│  └────────────────────────┬─────────────────────────────────┘ │
│                           │                                    │
│                  Need decision? ────┐                          │
│                           │         │                          │
│                           ↓         ↓                          │
│                  ┌──────────────────────────┐                 │
│                  │   IClaudeBrain Service   │                 │
│                  │  (Ephemeral API calls)   │                 │
│                  └──────────────────────────┘                 │
│                           │                                    │
│                           ↓                                    │
│                  Claude API "Think" Calls:                     │
│                  - Classify input type                         │
│                  - Assess scope                                │
│                  - Review artifacts                            │
│                  - Handle ambiguity                            │
│                  - Failure analysis                            │
└───────────────────────────────────────────────────────────────┘
                           │
              ┌────────────┼────────────┐
              │            │            │
              ▼            ▼            ▼
        ┌─────────┐  ┌─────────┐  ┌─────────┐
        │Architect│  │Developer│  │Reviewer │
        │ Agent   │  │ Agent   │  │ Agent   │
        └─────────┘  └─────────┘  └─────────┘
```

### Core Principles

1. **State in SQL, Intelligence on Demand**: All state lives in SQLite. Claude is called only for decisions requiring judgment.
2. **Hub-and-Spoke Communication**: All messages flow through PM. No agent-to-agent communication.
3. **Scope-Aware Routing**: PM selects agents and models based on assessed scope.
4. **Structured Context**: Rich context objects passed to agents, not generic prompts.

---

## Core Components

### 1. IClaudeBrain Service

Abstraction for ephemeral Claude API calls.

```csharp
public interface IClaudeBrain
{
    /// <summary>
    /// Make an ephemeral Claude call for a specific decision.
    /// Fresh context every call - no state accumulation.
    /// </summary>
    /// <typeparam name="T">Expected response type (must be JSON-serializable)</typeparam>
    /// <param name="prompt">Decision prompt</param>
    /// <param name="modelHint">Model preference: "haiku", "sonnet", or null for default</param>
    Task<T> ThinkAsync<T>(string prompt, string? modelHint = null);
}

public class ClaudeApiBrain : IClaudeBrain
{
    private readonly IOptions<ClaudeBrainOptions> _options;
    private readonly ILogger<ClaudeApiBrain> _logger;

    public async Task<T> ThinkAsync<T>(string prompt, string? modelHint = null)
    {
        // Build Claude API request with tool_choice for structured output
        // Call Anthropic API directly
        // Parse and return structured response
    }
}
```

**Configuration:**
```json
"ClaudeBrain": {
  "ApiKey": "<from-user-secrets>",
  "DefaultModel": "claude-sonnet-4-5",
  "MaxTokens": 4000,
  "Temperature": 1.0,
  "TimeoutSeconds": 60
}
```

### 2. PmOrchestrator Service

C# BackgroundService that coordinates agents.

```csharp
public class PmOrchestrator : BackgroundService
{
    private readonly IClaudeBrain _brain;
    private readonly IWorkUnitStore _store;
    private readonly IAgentSpawner _spawner;
    private readonly IMessageBus _messageBus;
    private readonly IContextGatherer _contextGatherer;
    private readonly IWorkflowSelector _workflowSelector;
    private readonly IAgentSelector _agentSelector;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // 1. Initial classification
        var input = await GetUserInputAsync();
        var classification = await ClassifyInputAsync(input);

        // 2. Gather context
        var projectContext = await _contextGatherer.GatherContextAsync();

        // 3. Assess scope
        var scope = await AssessScopeAsync(input, projectContext);

        // 4. Select workflow
        var workflow = _workflowSelector.SelectWorkflow(classification, scope);

        // 5. Create project plan
        var plan = await CreateProjectPlanAsync(workflow, scope);

        // 6. Event loop
        await RunEventLoopAsync(plan, ct);
    }

    private async Task<WorkflowType> ClassifyInputAsync(string input)
    {
        var prompt = $"""
            Classify this input:

            {input}

            Return JSON:
            {{
                "type": "NEW_PROJECT" | "FEATURE_REQUEST" | "BUG_FIX",
                "reason": "brief explanation"
            }}
            """;

        return await _brain.ThinkAsync<ClassificationResult>(prompt, "haiku");
    }

    private async Task RunEventLoopAsync(ProjectPlan plan, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !plan.IsComplete)
        {
            // Check for agent messages
            var messages = await _messageBus.GetUnprocessedMessagesAsync();

            foreach (var msg in messages)
            {
                await ProcessMessageAsync(msg, plan);
            }

            // Check for new work to assign
            await AssignPendingWorkAsync(plan);

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
```

### 3. Input Classification

```csharp
public enum WorkflowType
{
    NewProject,      // PROJECT-BRIEF.md → full pipeline
    FeatureRequest,  // Feature/issue → scoped pipeline
    QuickFix         // Bug/typo → minimal pipeline
}

public enum IssueScope
{
    Trivial,  // Single-line fix
    Small,    // Single-file change
    Medium,   // Multi-file, single component
    Large     // Architectural, multiple components
}

public class ScopeAssessment
{
    public IssueScope Scope { get; set; }
    public int EstimatedFiles { get; set; }
    public List<string> ComponentsAffected { get; set; }
    public string Reasoning { get; set; }
}
```

### 4. Context Gathering

```csharp
public interface IContextGatherer
{
    Task<ProjectContext> GatherContextAsync();
}

public class ProjectContext
{
    // From CLAUDE.md (if exists)
    public string? ClaudeMd { get; set; }
    public string Architecture { get; set; }           // "Three-layer: Core → Infra → App"
    public Dictionary<string, string> KeyPaths { get; set; }
    public Dictionary<string, string> Patterns { get; set; }
    public List<string> BuildCommands { get; set; }

    // Discovered or inferred
    public string ProjectType { get; set; }            // "dotnet", "node", "python"
    public bool HasTests { get; set; }
    public bool HasCI { get; set; }
}

public class ContextGatherer : IContextGatherer
{
    public async Task<ProjectContext> GatherContextAsync()
    {
        var context = new ProjectContext();

        // Try to read CLAUDE.md
        if (File.Exists("CLAUDE.md"))
        {
            context.ClaudeMd = await File.ReadAllTextAsync("CLAUDE.md");
            context.Architecture = ExtractArchitecture(context.ClaudeMd);
            context.KeyPaths = ExtractKeyPaths(context.ClaudeMd);
            context.Patterns = ExtractPatterns(context.ClaudeMd);
        }
        else
        {
            // Use brain to analyze codebase structure
            context = await AnalyzeCodebaseStructureAsync();
        }

        return context;
    }
}
```

### 5. Structured Context for Agents

```csharp
public class StructuredContext
{
    // Task specifics
    public string TaskSummary { get; set; }
    public List<string> Requirements { get; set; }

    // Project context
    public string Architecture { get; set; }
    public Dictionary<string, string> KeyPaths { get; set; }
    public Dictionary<string, string> Patterns { get; set; }

    // Specific guidance
    public List<string> FilesToModify { get; set; }
    public List<string> FilesToReadFirst { get; set; }
    public string? PatternReference { get; set; }      // "Follow src/Services/UserService.cs:45-80"
    public List<string> Constraints { get; set; }

    // From previous agents
    public string? SpecArtifactPath { get; set; }
    public string? DesignArtifactPath { get; set; }
    public ReviewFeedback? RevisionFeedback { get; set; }
}
```

### 6. Bidirectional Communication

```csharp
public enum AgentMessageType
{
    // Completion
    WorkCompleted,

    // Review outcomes
    ReviewApproved,
    ReviewChangesRequested,

    // Test outcomes
    TestsPassed,
    TestsFailed,

    // Blockers
    WorkBlocked,
    NeedsClarification,
    RequestHumanInput
}

public class AgentReport
{
    public string AgentRole { get; set; }
    public AgentMessageType Type { get; set; }
    public string Component { get; set; }
    public object Payload { get; set; }  // Type-specific data
}

public enum PmInstructionType
{
    Continue,
    Pause,
    Revise,
    Abort,
    ExpandScope,
    ReduceScope
}
```

### 7. Scope-Aware Agent Selection

```csharp
public class AgentAssignment
{
    public string AgentRole { get; set; }
    public string SubagentType { get; set; }
    public string ModelHint { get; set; }
    public StructuredContext Context { get; set; }
}

public interface IAgentSelector
{
    AgentAssignment SelectAgent(IssueScope scope, WorkUnitType type, ProjectContext projectContext);
}

public class AgentSelector : IAgentSelector
{
    public AgentAssignment SelectAgent(IssueScope scope, WorkUnitType type, ProjectContext context)
    {
        return (scope, type) switch
        {
            (Trivial or Small, Implementation) => new AgentAssignment
            {
                AgentRole = "developer",
                SubagentType = "dotnet-fixer",
                ModelHint = "haiku"
            },

            (Medium, Implementation) => new AgentAssignment
            {
                AgentRole = "developer",
                SubagentType = "dotnet-specialist",
                ModelHint = "sonnet"
            },

            (Large, Implementation) => new AgentAssignment
            {
                AgentRole = "developer",
                SubagentType = "dotnet-specialist",
                ModelHint = "sonnet"
            },

            (_, Review) => new AgentAssignment
            {
                AgentRole = "reviewer",
                SubagentType = "code-reviewer",
                ModelHint = "sonnet"
            },

            _ => throw new NotSupportedException($"No agent mapping for {scope}, {type}")
        };
    }
}
```

### 8. Review Loop Protocol

```csharp
public enum ReviewVerdict
{
    Approved,
    ChangesRequested
}

public class ReviewFeedback
{
    public ReviewVerdict Verdict { get; set; }
    public List<ReviewIssue> CriticalIssues { get; set; }
    public List<ReviewIssue> MajorIssues { get; set; }
    public List<ReviewIssue> MinorIssues { get; set; }
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

---

## Data Models

### Updated WorkUnit Entity

```csharp
public class WorkUnit
{
    public required string Id { get; set; }
    public required string ProjectId { get; set; }
    public required string Component { get; set; }
    public required WorkUnitType Type { get; set; }
    public WorkUnitStatus Status { get; set; }

    public string? AssignedAgent { get; set; }
    public string? AssignedSubagentType { get; set; }  // NEW
    public string? ModelHint { get; set; }             // NEW

    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? ArtifactPath { get; set; }
    public string? ParentWorkUnitId { get; set; }
    public string? StructuredContextJson { get; set; }  // NEW: Serialized StructuredContext

    public int RevisionCount { get; set; }
    public string? RevisionFeedbackJson { get; set; }   // NEW: Serialized ReviewFeedback
}
```

### New MCP Tools

#### apmas_report_to_pm

Replaces generic status updates with structured reporting.

```json
{
  "name": "apmas_report_to_pm",
  "description": "Report substantive updates to the PM (completion, review results, test results, blockers)",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": { "type": "string" },
      "messageType": {
        "type": "string",
        "enum": ["work_completed", "review_approved", "review_changes_requested",
                 "tests_passed", "tests_failed", "work_blocked", "needs_clarification"]
      },
      "component": { "type": "string" },
      "payload": { "type": "object" }
    },
    "required": ["agentRole", "messageType", "component", "payload"]
  }
}
```

#### apmas_pm_instruct

PM sends instructions to running agents.

```json
{
  "name": "apmas_pm_instruct",
  "description": "PM sends instructions to an agent (internal use only)",
  "inputSchema": {
    "type": "object",
    "properties": {
      "toAgent": { "type": "string" },
      "instructionType": {
        "type": "string",
        "enum": ["continue", "pause", "revise", "abort", "expand_scope", "reduce_scope"]
      },
      "instructions": { "type": "string" },
      "context": { "type": "object" }
    },
    "required": ["toAgent", "instructionType", "instructions"]
  }
}
```

---

## Workflow Examples

### Small Feature Request

**Input:** "Add dark mode toggle to settings page"

```
1. PM receives input

2. Brain "thinks": Classify
   → FEATURE_REQUEST (existing codebase, specific UI feature)

3. PM gathers context
   → Reads CLAUDE.md, extracts patterns

4. Brain "thinks": Assess scope
   → SMALL (single component, UI change, clear requirements)

5. PM selects workflow
   → Skip Architect (not needed)
   → Developer → Reviewer → PR

6. PM spawns Developer (dotnet-fixer, haiku) with StructuredContext:
   - Task: "Add dark mode toggle to settings"
   - Files: ["Pages/Settings.razor", "Shared/ThemeProvider.razor"]
   - Pattern: "Follow toggle pattern in Pages/Preferences.razor"

7. Developer reports: WorkCompleted

8. PM spawns Reviewer with context

9. Reviewer reports: ReviewApproved (minor issues noted but acceptable)

10. PM creates PR → Done
```

### Medium Feature with Review Loop

**Input:** GitHub issue #123 "Implement user authentication"

```
1. PM: Classify → FEATURE_REQUEST
2. PM: Assess scope → MEDIUM (multiple files, security-critical)
3. PM: Select workflow → Architect → Developer → Reviewer → Tester
4. PM: Create plan with components: ["auth-service", "auth-middleware"]

5. Spawn Architect (systems-architect, sonnet)
6. Architect submits spec:auth-service
7. Brain "thinks": Review spec → Minor issues found
8. PM: Approve with notes for Developer

9. Spawn Developer (dotnet-specialist, sonnet)
10. Developer submits impl:auth-service
11. Spawn Reviewer
12. Reviewer reports: ReviewChangesRequested (critical: missing input validation)

13. PM sends Revise instruction to Developer with feedback
14. Developer revises and resubmits
15. Reviewer reports: ReviewApproved

16. Spawn Tester
17. Tester reports: TestsPassed

18. PM creates PR → Done
```

---

## Implementation Phases

### Phase 1: Brain Infrastructure (NEW)
**Duration:** 2-3 days

- Create `IClaudeBrain` interface
- Implement `ClaudeApiBrain` with Anthropic API
- Add `ClaudeBrainOptions` configuration
- Basic "think" call with structured output
- Unit tests with mock brain

**Deliverable:** Working brain service that can make ephemeral Claude calls

### Phase 2: Work Unit Foundation (UNCHANGED)
**Duration:** 3-4 days

- Create WorkUnit and ProjectPlan entities (with new fields)
- Add database migrations
- Implement `IWorkUnitStore` interface
- Create `apmas_create_plan` tool
- Create `apmas_submit_work` tool (now stores StructuredContext)
- Create `apmas_get_work_status` tool
- Unit tests

**Deliverable:** Work units with structured context support

### Phase 3: Input Handling & Context (NEW)
**Duration:** 3-4 days

- Input classification logic
- `IContextGatherer` and implementation
- Scope assessment with brain
- `IWorkflowSelector` and implementation
- Project plan generation from workflow

**Deliverable:** PM can classify input and generate execution plan

### Phase 4: Bidirectional Communication (ENHANCED)
**Duration:** 3-4 days

- `apmas_report_to_pm` tool
- `apmas_pm_instruct` tool (internal)
- PM event loop message processing
- `StructuredContext` model and builder
- Agent prompt updates to use structured context

**Deliverable:** Agents can report structured updates and receive instructions

### Phase 5: Intelligent Routing (NEW)
**Duration:** 3-4 days

- `IAgentSelector` and implementation
- Model hints (haiku vs sonnet)
- Review iteration enforcement
- Dynamic workflow execution
- Brain-powered artifact review

**Deliverable:** PM routes work intelligently based on scope

### Phase 6: Orchestrator Supervisor (MODIFIED)
**Duration:** 2-3 days

- `OrchestratorOptions` configuration
- `ExecuteOrchestratorModeAsync` (spawns PM service, not agent)
- Agent spawn-on-demand
- Project completion detection
- Backward compatibility with pipeline mode

**Deliverable:** Full orchestrator mode working end-to-end

### Phase 7: Polish (UNCHANGED)
**Duration:** 2-3 days

- Concurrent agent execution
- Progress reporting
- Webhook notifications
- Dashboard events

**Deliverable:** Production-ready orchestrator

---

## What Changed vs Original Plan

| Aspect | Original Plan (Issue #89) | V2 Enhancement |
|--------|---------------------------|----------------|
| **PM Implementation** | Long-running Claude agent | C# service + ephemeral brain calls |
| **Input** | GitHub issue number only | Any input (brief, issue, bug) |
| **Classification** | Implicit (always full pipeline) | Explicit (project/feature/bug) |
| **Scope Assessment** | Not included | Trivial/Small/Medium/Large |
| **Agent Selection** | Fixed roster | Scope-aware (e.g., haiku for small) |
| **Context Passing** | Generic prompts | Structured context objects |
| **PM State** | Claude session context | SQL database |
| **Communication** | One-way (agent → PM) | Bidirectional (PM ↔ agents) |
| **Review Protocol** | Basic approve/revision | Structured feedback with iteration limits |

---

## What Stays the Same

1. **Work Unit Foundation** - Still needed, with added fields
2. **Agent Incremental Mode** - Agents still work on individual work units
3. **Pipelined Parallelism** - Architect → Developer → Reviewer pipeline
4. **MCP Tools** - Core tools like `apmas_heartbeat`, `apmas_checkpoint` unchanged
5. **Dependency Resolution** - Still needed for agent dependencies
6. **Timeout Handling** - Unchanged
7. **Agent Prompts** - Updated for structured context but same roles

---

## Configuration Example

```json
{
  "Apmas": {
    "ProjectName": "my-project",
    "WorkingDirectory": "C:/projects/my-project",

    "ClaudeBrain": {
      "ApiKey": "<from-user-secrets>",
      "DefaultModel": "claude-sonnet-4-5",
      "MaxTokens": 4000,
      "Temperature": 1.0
    },

    "Orchestrator": {
      "Enabled": true,
      "MaxReviewIterations": 2,
      "SkipMinorIssuesInFeedback": true,
      "RequireTestsPass": true
    },

    "Agents": {
      "Roster": [
        {
          "Role": "architect",
          "SubagentType": "systems-architect",
          "PromptType": "ArchitectPrompt"
        },
        {
          "Role": "developer",
          "SubagentTypes": ["dotnet-fixer", "dotnet-specialist"],
          "PromptType": "DeveloperPrompt"
        },
        {
          "Role": "reviewer",
          "SubagentType": "code-reviewer",
          "PromptType": "ReviewerPrompt"
        }
      ]
    }
  }
}
```

---

## Open Questions

1. **Brain Tool Use**: Should `IClaudeBrain.ThinkAsync` support tool use, or only text→structured output?
   - **Recommendation**: Start with structured output only. Add tool use if needed.

2. **PM Restart**: How to handle PM service crash mid-execution?
   - **Recommendation**: PM state is in SQL. On restart, resume from last known state.

3. **Agent-Requested Thinks**: Should agents be able to request PM to "think" about something?
   - **Recommendation**: Yes, via `apmas_request_help` with `helpType: "pm_decision"`.

4. **Cost Tracking**: Track costs of brain calls?
   - **Recommendation**: Yes. Add `BrainCallLog` table with token usage.

5. **Timeout for Think Calls**: What if brain call hangs?
   - **Recommendation**: 60s timeout configurable. Retry once, then fail gracefully.

---

## Success Metrics

- PM successfully classifies 95%+ of inputs correctly
- Scope assessment accuracy within 1 level (Small vs Medium) 90%+ of time
- Average brain calls per project: 5-10 (initial analysis + reviews)
- No context exhaustion failures
- 50% faster than sequential pipeline (unchanged goal)

---

*Specification Version: 2.0*
*Last Updated: 2026-01-22*
