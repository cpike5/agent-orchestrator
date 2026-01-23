# GitHub Issue Updates for PM Orchestrator v2

**Date:** 2026-01-22
**Based On:** [pm-orchestrator-spec-v2.md](pm-orchestrator-spec-v2.md)
**Parent Issue:** #89

---

## Summary of Changes

The PM Orchestrator enhancement evolves the PM from a long-running Claude agent to a C# service with ephemeral "think" calls. This requires:

- **New issues** for brain infrastructure, input classification, context gathering, and intelligent routing
- **Modified issues** for PM prompt, orchestrator execution, and agent prompts
- **Unchanged issues** for work unit foundation and most agent updates

---

## Issues to KEEP (Unchanged)

These issues remain valid with no or minimal changes:

### Phase 1: Work Unit Foundation
- **#63** - Create WorkUnit and ProjectPlan entities
  - **Minor update**: Add new fields (`AssignedSubagentType`, `ModelHint`, `StructuredContextJson`, `RevisionFeedbackJson`)
- **#64** - Add database migrations for WorkUnit tables
  - **Minor update**: Include new columns in migration
- **#65** - Implement IWorkUnitStore interface
  - No changes
- **#66** - Create apmas_create_plan MCP tool
  - No changes
- **#67** - Create apmas_submit_work MCP tool
  - **Minor update**: Accept and store StructuredContext
- **#68** - Create apmas_get_work_status MCP tool
  - No changes
- **#69** - Unit tests for work unit operations
  - No changes

### Phase 3: Agent Incremental Mode (Prompts)
- **#74** - Update ArchitectPrompt for incremental specs
  - **Minor update**: Use StructuredContext instead of generic instructions
- **#75** - Update DeveloperPrompt for work unit assignments
  - **Minor update**: Use StructuredContext with FilesToModify, PatternReference, etc.
- **#76** - Update ReviewerPrompt for incremental reviews
  - **Minor update**: Return structured ReviewFeedback
- **#77** - Update TesterPrompt for incremental tests
  - No changes
- **#78** - Agent revision request handling
  - **Minor update**: Handle StructuredContext in revision loops

### Phase 5: Polish
- **#84** - Concurrent agent execution support
- **#85** - Work unit dependency validation
- **#86** - Progress reporting improvements
- **#87** - External webhook notifications
- **#88** - Dashboard events for work unit status

### UI/Design Agent Updates
- **#90** - Update DesignerPrompt for orchestrator mode
- **#91** - Update PrototyperPrompt for orchestrator mode
- **#92** - Update UiCriticPrompt for orchestrator mode
- **#93** - UI workflow integration in PM

---

## Issues to MODIFY

### #70: Create ProjectManagerCoordinatorPrompt

**Current Description:** Long-running agent prompt with coordination loop

**CHANGE TO:**

**Title:** Create Initial Classification Prompt (PM Analysis Phase)

**Description:**
The PM Orchestrator is now a C# service, not a Claude agent. However, we still need a prompt for the **initial analysis phase** where the PM uses the brain to:
1. Classify the input (project/feature/bug)
2. Identify components
3. Make initial planning decisions

This is NOT a long-running coordination loop. It's a one-time "think" call to bootstrap the project plan.

**Updated Acceptance Criteria:**
- [ ] Prompt template for input classification
- [ ] Prompt template for component identification
- [ ] Prompt template for scope assessment
- [ ] Returns structured JSON, not free-form text
- [ ] Used by `PmOrchestrator.ClassifyInputAsync()` and related methods

**Files to Create:**
- `src/Apmas.Server/Orchestrator/Prompts/ClassificationPrompt.cs`
- `src/Apmas.Server/Orchestrator/Prompts/ScopeAssessmentPrompt.cs`
- `src/Apmas.Server/Orchestrator/Prompts/ComponentIdentificationPrompt.cs`

---

### #71: Create apmas_route_work MCP tool

**Current Description:** PM tool for routing work after review

**UPDATE:**

**Additional Notes:**
This tool is now **internal to PM service**. It's not called by the PM agent (since PM is not an agent), but by the PM event loop when processing agent reports.

The tool logic moves to `PmOrchestrator.RouteWorkUnitAsync()` method. The MCP tool wrapper may still exist for testing or manual intervention, but it's not the primary interface.

**Updated Acceptance Criteria:**
- [ ] `RouteWorkUnitAsync()` method in PmOrchestrator
- [ ] Handles approve/revision decisions
- [ ] Creates next work unit in pipeline
- [ ] Updates work unit status
- [ ] Sends instructions to agents via `apmas_pm_instruct`

---

### #72: Update PromptFactory for orchestrator prompts

**Current Description:** Update PromptFactory for PM coordination prompt

**UPDATE:**

**Title:** Update PromptFactory for StructuredContext-based prompts

**Description:**
PromptFactory now generates prompts FROM StructuredContext objects, not from generic templates.

**Updated Acceptance Criteria:**
- [ ] `IPromptFactory.CreatePrompt(string promptType, StructuredContext context)` signature
- [ ] Prompts include specific file paths, patterns, and constraints from context
- [ ] No generic "explore the codebase" instructions
- [ ] Prompts reference exact interface files, example patterns

**Example:**
```csharp
var context = new StructuredContext
{
    TaskSummary = "Implement authentication service",
    FilesToModify = ["src/Services/AuthService.cs"],
    FilesToReadFirst = ["src/Interfaces/IAuthService.cs"],
    PatternReference = "Follow pattern in src/Services/UserService.cs:45-80",
    Architecture = "Three-layer: Core → Infrastructure → Application"
};

var prompt = factory.CreatePrompt("DeveloperPrompt", context);
// Prompt contains specific guidance, not generic instructions
```

---

### #79: Add OrchestratorOptions configuration

**Current Description:** Basic orchestrator configuration

**UPDATE:**

**Additional Options:**
```json
"Orchestrator": {
  "Enabled": true,
  "MaxReviewIterations": 2,
  "SkipMinorIssuesInFeedback": true,
  "RequireTestsPass": true,
  "InputSource": "github_issue",  // NEW: or "project_brief", "stdin"
  "IssueNumber": 123               // NEW: If InputSource is github_issue
}
```

---

### #80: Implement ExecuteOrchestratorModeAsync in SupervisorService

**Current Description:** Spawn PM agent and coordinate

**MAJOR CHANGE:**

**Title:** Implement ExecuteOrchestratorModeAsync - PM Service Coordination

**Description:**
The PM is now a C# service (`PmOrchestrator`), not a spawned Claude agent. The Supervisor's role changes:

**OLD:** Spawn long-running PM agent, monitor it
**NEW:** Instantiate and run `PmOrchestrator` service directly

**Updated Implementation:**
```csharp
private async Task ExecuteOrchestratorModeAsync(CancellationToken ct)
{
    // NO agent spawning for PM
    // PM is a C# service that runs in this method

    var pmService = _serviceProvider.GetRequiredService<PmOrchestrator>();

    // Run PM orchestrator
    await pmService.ExecuteAsync(ct);

    // PM service handles:
    // - Input classification
    // - Context gathering
    // - Plan creation
    // - Agent spawning
    // - Event loop coordination
    // - Project completion
}
```

**Updated Acceptance Criteria:**
- [ ] No PM agent spawning (PM is C# service)
- [ ] Instantiate `PmOrchestrator` from DI
- [ ] Run `PmOrchestrator.ExecuteAsync()`
- [ ] Supervisor monitors spawned agents (not PM)
- [ ] Project completion detected via PM service

---

### #81: Agent spawn-on-demand for work units

**UPDATE:**

**Additional Note:**
PM service (not PM agent) requests agent spawning. The `IAgentSpawner` is injected into `PmOrchestrator`.

---

### #82: PM health monitoring and recovery

**MAJOR CHANGE:**

**Title:** PM Service Health Monitoring (not agent monitoring)

**Description:**
Since PM is a C# service running in the Supervisor, we don't monitor it like an agent. Instead:

- If PM service throws an exception, the entire Supervisor fails (by design - this is catastrophic)
- PM service should have internal error handling for recoverable issues
- Use health checks to verify PM service is responsive

**Updated Acceptance Criteria:**
- [ ] PM service exception handling (don't crash on recoverable errors)
- [ ] Health check endpoint for PM service status
- [ ] Metrics for PM brain call failures
- [ ] Graceful degradation if brain unavailable (queue decisions for retry)

---

### #83: Project completion detection

**UPDATE:**

No changes needed. PM service detects completion when all work units are in `Completed` status.

---

## Issues to CREATE (New)

### NEW: Create IClaudeBrain Interface and Options

**Title:** Create IClaudeBrain interface and configuration

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 1 (Brain Infrastructure)

**Description:**
Create the abstraction for ephemeral Claude API calls that the PM service uses for decision-making.

This is the core innovation: PM is a C# service that calls Claude only when intelligence is needed, rather than being a long-running Claude agent.

**Acceptance Criteria:**
- [ ] `IClaudeBrain` interface with `ThinkAsync<T>()` method
- [ ] `ClaudeBrainOptions` configuration class
- [ ] Interface supports model hints (haiku vs sonnet)
- [ ] Interface supports timeout configuration
- [ ] Returns structured typed responses (JSON deserialization)

**Implementation Notes:**
```csharp
public interface IClaudeBrain
{
    Task<T> ThinkAsync<T>(string prompt, string? modelHint = null);
}

public class ClaudeBrainOptions
{
    public string ApiKey { get; set; }
    public string DefaultModel { get; set; } = "claude-sonnet-4-5";
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 1.0;
    public int TimeoutSeconds { get; set; } = 60;
}
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IClaudeBrain.cs`
- `src/Apmas.Server/Configuration/ClaudeBrainOptions.cs`

---

### NEW: Implement ClaudeApiBrain with Anthropic API

**Title:** Implement ClaudeApiBrain using Anthropic SDK

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 1 (Brain Infrastructure)

**Dependencies:** Previous issue (IClaudeBrain interface)

**Description:**
Implement the brain service using the Anthropic .NET SDK (or direct HTTP API if SDK not available).

Uses tool_choice for structured output (JSON mode).

**Acceptance Criteria:**
- [ ] `ClaudeApiBrain` implements `IClaudeBrain`
- [ ] Uses Anthropic API client
- [ ] Supports model selection (haiku, sonnet)
- [ ] Enforces structured output via tool_choice
- [ ] Handles API errors gracefully (retry, timeout)
- [ ] Logs token usage for cost tracking
- [ ] Unit tests with mocked API

**Implementation Notes:**
- Use `Anthropic.SDK` NuGet package (if available) or direct HttpClient
- For structured output: use `tools` parameter with single tool, `tool_choice: {"type": "tool", "name": "respond"}`
- Parse tool_use response and deserialize to `T`

**Files to Create:**
- `src/Apmas.Server/Orchestrator/ClaudeApiBrain.cs`
- `tests/Apmas.Server.Tests/Orchestrator/ClaudeApiBrainTests.cs`

---

### NEW: Create Input Classification Service

**Title:** Implement input classification logic with brain

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 3 (Input Handling & Context)

**Description:**
Classify user input into workflow types (NEW_PROJECT, FEATURE_REQUEST, BUG_FIX) using the brain.

**Acceptance Criteria:**
- [ ] `IInputClassifier` interface
- [ ] `InputClassifier` implementation using `IClaudeBrain`
- [ ] Classification prompt template
- [ ] Returns `WorkflowType` enum
- [ ] Handles ambiguous inputs (defaults to FEATURE_REQUEST)
- [ ] Unit tests with mock brain

**Implementation Notes:**
```csharp
public interface IInputClassifier
{
    Task<WorkflowType> ClassifyAsync(string input);
}

public enum WorkflowType
{
    NewProject,
    FeatureRequest,
    QuickFix
}
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IInputClassifier.cs`
- `src/Apmas.Server/Orchestrator/InputClassifier.cs`
- `src/Apmas.Server/Orchestrator/Prompts/ClassificationPrompt.cs`

---

### NEW: Create Context Gathering Service

**Title:** Implement IContextGatherer for project context extraction

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 3 (Input Handling & Context)

**Description:**
Service that reads CLAUDE.md (if exists) or analyzes codebase structure to gather project context.

This context is used to populate StructuredContext for agents.

**Acceptance Criteria:**
- [ ] `IContextGatherer` interface
- [ ] `ContextGatherer` implementation
- [ ] Reads and parses CLAUDE.md
- [ ] Extracts architecture, key paths, patterns, build commands
- [ ] Falls back to brain analysis if no CLAUDE.md
- [ ] Returns `ProjectContext` model
- [ ] Unit tests

**Implementation Notes:**
```csharp
public class ProjectContext
{
    public string? ClaudeMd { get; set; }
    public string Architecture { get; set; }
    public Dictionary<string, string> KeyPaths { get; set; }
    public Dictionary<string, string> Patterns { get; set; }
    public List<string> BuildCommands { get; set; }
    public string ProjectType { get; set; }  // "dotnet", "node", etc.
}
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IContextGatherer.cs`
- `src/Apmas.Server/Orchestrator/ContextGatherer.cs`
- `src/Apmas.Server/Core/Models/ProjectContext.cs`

---

### NEW: Create Scope Assessment Service

**Title:** Implement scope assessment with brain

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 3 (Input Handling & Context)

**Description:**
Assess the scope of a feature/bug (TRIVIAL/SMALL/MEDIUM/LARGE) using the brain, given the input and project context.

**Acceptance Criteria:**
- [ ] `IScopeAssessor` interface
- [ ] `ScopeAssessor` implementation using brain
- [ ] Scope assessment prompt template
- [ ] Returns `IssueScope` enum and reasoning
- [ ] Considers codebase size, complexity
- [ ] Unit tests

**Implementation Notes:**
```csharp
public enum IssueScope
{
    Trivial,  // 1-line fix
    Small,    // 1 file
    Medium,   // Multi-file, 1 component
    Large     // Architectural, multi-component
}

public class ScopeAssessment
{
    public IssueScope Scope { get; set; }
    public int EstimatedFiles { get; set; }
    public List<string> ComponentsAffected { get; set; }
    public string Reasoning { get; set; }
}
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IScopeAssessor.cs`
- `src/Apmas.Server/Orchestrator/ScopeAssessor.cs`
- `src/Apmas.Server/Orchestrator/Prompts/ScopeAssessmentPrompt.cs`

---

### NEW: Create Workflow Selection Service

**Title:** Implement IWorkflowSelector for pipeline selection

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 3 (Input Handling & Context)

**Description:**
Select the appropriate workflow (agent pipeline) based on classification and scope.

This is deterministic logic (no brain needed).

**Acceptance Criteria:**
- [ ] `IWorkflowSelector` interface
- [ ] `WorkflowSelector` implementation
- [ ] Decision tree: (WorkflowType, IssueScope) → pipeline
- [ ] Returns list of work unit types needed
- [ ] Unit tests with all combinations

**Implementation Notes:**
```csharp
public interface IWorkflowSelector
{
    List<WorkUnitType> SelectWorkflow(WorkflowType type, IssueScope scope);
}

// Example:
(NEW_PROJECT, LARGE) → [Spec, Design, Prototype, Implementation, Review, Test]
(FEATURE_REQUEST, SMALL) → [Implementation, Review]
(BUG_FIX, TRIVIAL) → [Implementation]
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IWorkflowSelector.cs`
- `src/Apmas.Server/Orchestrator/WorkflowSelector.cs`

---

### NEW: Create apmas_report_to_pm MCP Tool

**Title:** Create apmas_report_to_pm tool for structured agent reporting

**Labels:** enhancement, component:mcp-tools, orchestrator

**Parent:** #89

**Phase:** 4 (Bidirectional Communication)

**Description:**
New MCP tool that replaces generic `apmas_report_status` for substantive updates.

Agents use this to report completion, review results, test results, and blockers with structured payloads.

**Acceptance Criteria:**
- [ ] `ReportToPmTool` class in `Mcp/Tools/`
- [ ] Accepts `AgentMessageType` enum
- [ ] Accepts component name and typed payload
- [ ] Publishes message to PM event loop
- [ ] Tool schema registered in MCP server
- [ ] Integration test

**JSON Schema:**
```json
{
  "name": "apmas_report_to_pm",
  "inputSchema": {
    "type": "object",
    "properties": {
      "agentRole": { "type": "string" },
      "messageType": {
        "type": "string",
        "enum": ["work_completed", "review_approved", "review_changes_requested",
                 "tests_passed", "tests_failed", "work_blocked"]
      },
      "component": { "type": "string" },
      "payload": { "type": "object" }
    }
  }
}
```

**Files to Create:**
- `src/Apmas.Server/Mcp/Tools/ReportToPmTool.cs`
- `src/Apmas.Server/Core/Models/AgentReport.cs`

---

### NEW: Create apmas_pm_instruct Internal Method

**Title:** Implement PM instruction sending to agents

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 4 (Bidirectional Communication)

**Description:**
PM service method to send instructions to running agents (continue, pause, revise, etc.).

This is an **internal method**, not an MCP tool (agents don't call it). PM calls it when processing agent reports.

**Acceptance Criteria:**
- [ ] `SendInstructionAsync()` method in PmOrchestrator
- [ ] Accepts `PmInstructionType` enum
- [ ] Sends message via `IMessageBus`
- [ ] Agent prompts updated to check for PM instructions
- [ ] Unit tests

**Implementation Notes:**
```csharp
public enum PmInstructionType
{
    Continue,
    Pause,
    Revise,
    Abort,
    ExpandScope,
    ReduceScope
}

public async Task SendInstructionAsync(string toAgent, PmInstructionType type, string instructions, object? context = null)
{
    await _messageBus.PublishAsync(new AgentMessage
    {
        From = "pm-orchestrator",
        To = toAgent,
        Type = MessageType.Instruction,
        Content = instructions,
        MetadataJson = JsonSerializer.Serialize(new { InstructionType = type, Context = context })
    });
}
```

**Files to Modify:**
- `src/Apmas.Server/Orchestrator/PmOrchestrator.cs`

---

### NEW: Create StructuredContext Model and Builder

**Title:** Implement StructuredContext for agent prompt generation

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 4 (Bidirectional Communication)

**Description:**
Rich context object that PM builds and passes to agents, containing specific file paths, patterns, constraints, and upstream artifacts.

**Acceptance Criteria:**
- [ ] `StructuredContext` model class
- [ ] `StructuredContextBuilder` fluent API
- [ ] Serializes to JSON for storage in WorkUnit
- [ ] PromptFactory uses StructuredContext to generate prompts
- [ ] Unit tests

**Implementation Notes:**
```csharp
public class StructuredContext
{
    public string TaskSummary { get; set; }
    public List<string> Requirements { get; set; }
    public string Architecture { get; set; }
    public Dictionary<string, string> KeyPaths { get; set; }
    public Dictionary<string, string> Patterns { get; set; }
    public List<string> FilesToModify { get; set; }
    public List<string> FilesToReadFirst { get; set; }
    public string? PatternReference { get; set; }
    public List<string> Constraints { get; set; }
    public string? SpecArtifactPath { get; set; }
    public string? DesignArtifactPath { get; set; }
    public ReviewFeedback? RevisionFeedback { get; set; }
}
```

**Files to Create:**
- `src/Apmas.Server/Core/Models/StructuredContext.cs`
- `src/Apmas.Server/Orchestrator/StructuredContextBuilder.cs`

---

### NEW: Create IAgentSelector Service

**Title:** Implement scope-aware agent and model selection

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 5 (Intelligent Routing)

**Description:**
Service that selects the appropriate agent subtype and model based on scope.

For example:
- TRIVIAL/SMALL → dotnet-fixer (haiku)
- MEDIUM/LARGE → dotnet-specialist (sonnet)

**Acceptance Criteria:**
- [ ] `IAgentSelector` interface
- [ ] `AgentSelector` implementation
- [ ] Decision table for (scope, work unit type) → agent assignment
- [ ] Returns `AgentAssignment` with role, subtype, model hint
- [ ] Configuration-driven (allow overrides)
- [ ] Unit tests

**Implementation Notes:**
```csharp
public class AgentAssignment
{
    public string AgentRole { get; set; }
    public string SubagentType { get; set; }
    public string ModelHint { get; set; }  // "haiku", "sonnet"
}
```

**Files to Create:**
- `src/Apmas.Server/Orchestrator/IAgentSelector.cs`
- `src/Apmas.Server/Orchestrator/AgentSelector.cs`
- `src/Apmas.Server/Core/Models/AgentAssignment.cs`

---

### NEW: Implement Review Iteration Enforcement

**Title:** Add review iteration tracking and enforcement

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 5 (Intelligent Routing)

**Description:**
PM tracks how many times a work unit has been revised and enforces a maximum (default: 2 iterations).

After max iterations, either:
1. Proceed with issues noted in PR
2. Escalate to human

**Acceptance Criteria:**
- [ ] `RevisionCount` tracked in WorkUnit
- [ ] `MaxReviewIterations` in OrchestratorOptions
- [ ] PM enforces limit before routing back to developer
- [ ] On limit exceeded: brain decides proceed vs escalate
- [ ] Unit tests

**Implementation Notes:**
- Check `workUnit.RevisionCount < _options.MaxReviewIterations` before sending Revise instruction
- If limit exceeded, use brain: "This work unit has failed review 3 times. Should we: (A) proceed with issues noted, or (B) escalate to human?"

**Files to Modify:**
- `src/Apmas.Server/Orchestrator/PmOrchestrator.cs` (routing logic)
- `src/Apmas.Server/Configuration/OrchestratorOptions.cs`

---

### NEW: Implement PmOrchestrator Event Loop

**Title:** Create PM event loop for message processing

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 4 (Bidirectional Communication)

**Description:**
The core PM coordination loop that:
1. Polls for agent messages
2. Processes reports (completion, review results, blockers)
3. Makes routing decisions (deterministic or brain-powered)
4. Assigns new work units
5. Spawns agents as needed

**Acceptance Criteria:**
- [ ] `RunEventLoopAsync()` method in PmOrchestrator
- [ ] Polls `IMessageBus` for unprocessed messages
- [ ] Routes work units based on agent reports
- [ ] Spawns agents via `IAgentSpawner`
- [ ] Detects project completion
- [ ] Integration test

**Implementation Notes:**
```csharp
private async Task RunEventLoopAsync(ProjectPlan plan, CancellationToken ct)
{
    while (!ct.IsCancellationRequested && !plan.IsComplete)
    {
        var messages = await _messageBus.GetUnprocessedMessagesAsync();

        foreach (var msg in messages)
        {
            await ProcessMessageAsync(msg, plan);
        }

        await AssignPendingWorkAsync(plan);

        await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }
}
```

**Files to Modify:**
- `src/Apmas.Server/Orchestrator/PmOrchestrator.cs`

---

### NEW: Create PmOrchestrator Service Bootstrap

**Title:** Create PmOrchestrator BackgroundService and DI registration

**Labels:** enhancement, component:orchestrator, orchestrator

**Parent:** #89

**Phase:** 3 (Input Handling & Context)

**Dependencies:** Brain infrastructure, input classification, context gathering

**Description:**
Create the main `PmOrchestrator` BackgroundService that orchestrates the entire project.

This service:
1. Receives input (from config or stdin)
2. Classifies input
3. Gathers context
4. Assesses scope
5. Selects workflow
6. Creates project plan
7. Runs event loop

**Acceptance Criteria:**
- [ ] `PmOrchestrator` class inheriting `BackgroundService`
- [ ] DI registration in `Program.cs`
- [ ] All dependencies injected (brain, classifiers, spawner, etc.)
- [ ] `ExecuteAsync()` method with full orchestration flow
- [ ] Logging and error handling

**Files to Create:**
- `src/Apmas.Server/Orchestrator/PmOrchestrator.cs`

**Files to Modify:**
- `src/Apmas.Server/Program.cs` (DI registration)

---

## Issues to CLOSE

None. All existing issues are still relevant, some with modifications.

---

## Recommended Implementation Order

### Phase 1: Brain Infrastructure (3-4 days)
1. Create IClaudeBrain interface
2. Implement ClaudeApiBrain
3. Add configuration and unit tests

### Phase 2: Work Unit Foundation (3-4 days)
Execute issues #63-69 with minor updates for new fields

### Phase 3: Input Handling & Context (4-5 days)
1. Create input classification service
2. Create context gathering service
3. Create scope assessment service
4. Create workflow selection service
5. Create PmOrchestrator bootstrap
6. Modify #80 (SupervisorService orchestrator mode)

### Phase 4: Bidirectional Communication (4-5 days)
1. Create StructuredContext model
2. Create apmas_report_to_pm tool
3. Implement PM instruction sending
4. Update PromptFactory (#72)
5. Implement PM event loop
6. Update agent prompts (#74-78) for StructuredContext

### Phase 5: Intelligent Routing (3-4 days)
1. Create IAgentSelector service
2. Implement review iteration enforcement
3. Brain-powered artifact review integration
4. Dynamic workflow execution

### Phase 6: UI/Design Integration (2-3 days)
Execute issues #90-93 (unchanged)

### Phase 7: Polish (2-3 days)
Execute issues #84-88 (unchanged)

---

## Summary Statistics

- **Total new issues:** 11
- **Modified issues:** 7
- **Unchanged issues:** 21
- **Closed issues:** 0
- **Total issues in plan:** 39 (was 30, added 9 net new)

---

## Next Steps for Architect

1. Review this document for accuracy and completeness
2. Create new GitHub issues using the templates below
3. Update modified issues with new descriptions
4. Update issue #89 body to reference v2 spec
5. Create milestones for new phases if needed
6. Assign issues to phases/milestones

---

## Issue Templates Ready for Creation

The following templates are ready to paste into GitHub. Each includes title, labels, body, and parent reference.

### Template Format

```markdown
**Title:** [Issue Title]
**Labels:** enhancement, component:orchestrator, orchestrator
**Milestone:** PM Orchestrator Phase N
**Parent:** Closes #89

[Issue body from sections above]
```

All new issue content is provided in the "Issues to CREATE" section above, ready for GitHub issue creation.

---

*Document Version: 1.0*
*Last Updated: 2026-01-22*
