# Discord Integration & Project Manager Agent Design

**Status:** Draft
**Created:** 2026-01-22
**Purpose:** Design for Discord bot integration with APMAS and new Project Manager agent

---

## Table of Contents

1. [Overview](#overview)
2. [Project Manager Agent](#project-manager-agent)
3. [Discord Bot Integration](#discord-bot-integration)
4. [Workflow Modifications](#workflow-modifications)
5. [Configuration Changes](#configuration-changes)
6. [Implementation Phases](#implementation-phases)
7. [Open Questions](#open-questions)

---

## Overview

### Goal

Enable Discord commands to trigger autonomous GitHub issue resolution:

```
User: @bot fix #25
Bot:  🔄 Starting work on issue #25...
Bot:  📋 PM Agent analyzing issue...
Bot:  🏗️ Architect designing solution...
Bot:  💻 Developer implementing...
Bot:  ✅ PR #42 created: https://github.com/org/repo/pull/42
```

### Components

| Component | Responsibility |
|-----------|----------------|
| Discord Bot | Receives commands, triggers APMAS, relays status |
| APMAS Server | Orchestrates agents, manages lifecycle |
| Project Manager Agent | Analyzes issues, creates specs, determines workflow |
| Existing Agents | Architect, Designer, Developer, Reviewer, Tester |

### Data Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Discord Server                                  │
│  User: @bot fix #25                                                         │
└─────────────────┬───────────────────────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              Discord Bot (.NET)                              │
│  1. Parse command                                                           │
│  2. Call APMAS API: POST /api/projects { issueNumber: 25 }                 │
│  3. Subscribe to status updates via webhook/polling                         │
│  4. Relay updates to Discord channel                                        │
└─────────────────┬───────────────────────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              APMAS Server                                    │
│  ┌─────────────────┐                                                        │
│  │ Project Manager │ ──▶ Fetches GitHub issue                               │
│  │     Agent       │ ──▶ Analyzes codebase                                  │
│  │                 │ ──▶ Creates spec + workflow recommendation             │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼ (spec created)                                                  │
│  ┌─────────────────┐     ┌─────────────────┐                               │
│  │   Architect     │────▶│    Designer     │ (if UI work needed)           │
│  └────────┬────────┘     └────────┬────────┘                               │
│           │                       │                                         │
│           └───────────┬───────────┘                                         │
│                       ▼                                                     │
│  ┌─────────────────────────────────┐                                        │
│  │          Developer              │                                        │
│  └────────────────┬────────────────┘                                        │
│                   │                                                         │
│           ┌───────┴───────┐                                                 │
│           ▼               ▼                                                 │
│  ┌─────────────┐   ┌─────────────┐                                         │
│  │  Reviewer   │   │   Tester    │                                         │
│  └─────────────┘   └─────────────┘                                         │
│           │               │                                                 │
│           └───────┬───────┘                                                 │
│                   ▼                                                         │
│  ┌─────────────────────────────────┐                                        │
│  │     Create PR (gh pr create)    │                                        │
│  └─────────────────────────────────┘                                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Manager Agent

### Agent Definition

```json
{
  "Role": "project-manager",
  "SubagentType": "general-purpose",
  "Dependencies": [],
  "Description": "Analyzes GitHub issues and creates implementation specifications",
  "PromptType": "ProjectManagerPrompt"
}
```

### Prompt: ProjectManagerPrompt.cs

```csharp
namespace Apmas.Server.Agents.Prompts;

public class ProjectManagerPrompt : BaseAgentPrompt
{
    public override string Role => "Project Manager";
    public override string SubagentType => "general-purpose";

    protected override string GetRoleDescription() => """
        You are the **Project Manager** responsible for analyzing GitHub issues and
        creating implementation specifications. You bridge user requests and technical
        implementation by understanding what needs to be done and how to approach it.

        You have strong skills in:
        - Reading and interpreting GitHub issues
        - Exploring codebases to understand architecture
        - Scoping work and identifying affected components
        - Writing clear, actionable specifications
        - Determining appropriate workflows based on issue complexity
        """;

    protected override string GetTaskDescription() => """
        ### Phase 1: Issue Analysis

        1. **Fetch the GitHub issue** using the `gh` CLI:
           ```bash
           gh issue view {ISSUE_NUMBER} --json title,body,labels,comments,assignees
           ```

        2. **Understand the request:**
           - What is the user asking for?
           - What problem are they trying to solve?
           - Are there acceptance criteria specified?
           - Are there any constraints or preferences mentioned?

        3. **Check for linked issues or PRs:**
           ```bash
           # Use the custom gh-view script for sub-issue relationships
           ~/.claude/scripts/gh-view -Issue {ISSUE_NUMBER}
           ```

        ### Phase 2: Codebase Exploration

        4. **Explore the codebase** to understand:
           - Current architecture and patterns
           - Files/components that will be affected
           - Existing similar implementations to follow
           - Test patterns in use

        5. **Identify scope:**
           - **Trivial (T):** Typo fix, config change, single-line fix
           - **Small (S):** Single file change, simple bug fix
           - **Medium (M):** Multi-file change, new feature in existing pattern
           - **Large (L):** New component, architectural change, cross-cutting concern

        ### Phase 3: Specification Creation

        6. **Create the implementation specification** at `docs/specs/issue-{NUMBER}-spec.md`

        7. **Determine the workflow** based on scope and type:
           - Backend-only work: Skip Designer
           - Trivial fixes: Developer only (skip Architect)
           - UI work: Full workflow with Designer
           - Test-only: Tester only

        ### Phase 4: Handoff

        8. **Report completion** with the spec file as artifact
        9. The spec will be used by downstream agents (Architect, Developer, etc.)
        """;

    protected override string GetDeliverables() => """
        Create `docs/specs/issue-{NUMBER}-spec.md` with this structure:

        ```markdown
        # Issue #{NUMBER}: {Title}

        ## Summary
        {One paragraph explaining what needs to be done and why}

        ## Original Issue
        - **Link:** {GitHub issue URL}
        - **Reporter:** {username}
        - **Labels:** {labels}

        ## Acceptance Criteria
        - [ ] {Criterion 1 - derived from issue or inferred}
        - [ ] {Criterion 2}
        - [ ] {Criterion 3}

        ## Scope Assessment
        - **Size:** {T/S/M/L}
        - **Type:** {bug-fix | feature | refactor | docs | test}
        - **Risk:** {low | medium | high}
        - **Estimated Agents:** {list of agents needed}

        ## Affected Components
        | Component | File(s) | Change Type |
        |-----------|---------|-------------|
        | {name}    | {path}  | {modify/create/delete} |

        ## Implementation Approach
        {Describe the recommended approach, including:
        - Key changes needed
        - Patterns to follow (reference existing code)
        - Potential gotchas or edge cases
        - Any dependencies or ordering constraints}

        ## Recommended Workflow
        {Based on scope, specify which agents should run:}

        ### Option A: Full Workflow (for L/M features with UI)
        project-manager → architect → designer → developer → reviewer + tester

        ### Option B: Backend Only (for M features, backend-only)
        project-manager → architect → developer → reviewer + tester

        ### Option C: Simple Fix (for S bug fixes)
        project-manager → developer → reviewer

        ### Option D: Trivial (for T changes)
        project-manager → developer (auto-merge if tests pass)

        **Selected:** {Option X}
        **Rationale:** {Why this workflow fits}

        ## Questions / Blockers
        {Any clarifications needed before proceeding - use apmas_request_help if blocking}

        ## References
        - {Link to relevant docs}
        - {Link to similar implementations}
        - {Link to related issues/PRs}
        ```
        """;

    protected override string GetDependencies() => """
        **No agent dependencies** - you are the first agent in the workflow.

        **External dependencies:**
        - GitHub issue must exist and be accessible via `gh` CLI
        - Repository must be cloned in the working directory
        """;
}
```

### Scope-Based Workflow Selection

The PM agent determines the workflow based on analysis:

| Scope | Type | Workflow | Agents |
|-------|------|----------|--------|
| Trivial | Any | D | PM → Developer |
| Small | Bug fix | C | PM → Developer → Reviewer |
| Small | Feature | C | PM → Developer → Reviewer + Tester |
| Medium | Backend | B | PM → Architect → Developer → Reviewer + Tester |
| Medium | Full-stack | A | PM → Architect → Designer → Developer → Reviewer + Tester |
| Large | Any | A | PM → Architect → Designer → Developer → Reviewer + Tester |

---

## Discord Bot Integration

### Command Syntax

```
@bot fix #25              # Fix GitHub issue #25
@bot fix #25 --fast       # Skip architect, go straight to developer
@bot fix #25 --full       # Force full workflow regardless of scope
@bot status               # Show current APMAS status
@bot cancel               # Cancel current workflow
@bot list                 # List recent/active workflows
```

### Bot → APMAS Communication

#### Option A: HTTP API (Recommended)

Add a minimal REST API to APMAS for external triggering:

```csharp
// New: Apmas.Server/Api/ProjectController.cs

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IProjectInitializer _initializer;
    private readonly IAgentStateManager _stateManager;

    [HttpPost]
    public async Task<ActionResult<ProjectStartResult>> StartProject(
        [FromBody] StartProjectRequest request)
    {
        // Validate GitHub issue exists
        // Initialize project with PM agent
        // Return project ID for status polling
    }

    [HttpGet("{projectId}/status")]
    public async Task<ActionResult<ProjectStatus>> GetStatus(string projectId)
    {
        // Return current phase, active agents, recent messages
    }

    [HttpPost("{projectId}/cancel")]
    public async Task<ActionResult> CancelProject(string projectId)
    {
        // Gracefully shutdown all agents
    }
}

public record StartProjectRequest
{
    public required int IssueNumber { get; init; }
    public string? Repository { get; init; }  // Default: current repo
    public WorkflowOverride? Workflow { get; init; }  // --fast, --full
    public string? CallbackUrl { get; init; }  // Webhook for status updates
}

public record ProjectStartResult
{
    public required string ProjectId { get; init; }
    public required string StatusUrl { get; init; }
}
```

#### Option B: CLI Invocation

Bot spawns APMAS as a subprocess:

```bash
dotnet run --project src/Apmas.Server -- \
  --issue 25 \
  --callback "https://bot.example.com/webhook/apmas"
```

### APMAS → Bot Communication

#### Webhook Callbacks

APMAS posts status updates to the bot's callback URL:

```csharp
public interface IExternalNotifier
{
    Task NotifyStatusChangeAsync(ProjectStatusUpdate update);
}

public record ProjectStatusUpdate
{
    public required string ProjectId { get; init; }
    public required string Phase { get; init; }  // "analyzing", "architecting", "developing", etc.
    public required string Message { get; init; }
    public string? ActiveAgent { get; init; }
    public double? ProgressPercent { get; init; }
    public string? PullRequestUrl { get; init; }  // When PR is created
    public string? Error { get; init; }
}
```

#### Discord Bot Webhook Handler

```csharp
// In Discord bot project

[HttpPost("webhook/apmas")]
public async Task<IActionResult> HandleApmasUpdate([FromBody] ProjectStatusUpdate update)
{
    var channel = await _discord.GetChannelAsync(_config.StatusChannelId);

    var embed = update.Phase switch
    {
        "analyzing" => BuildAnalyzingEmbed(update),
        "architecting" => BuildArchitectingEmbed(update),
        "developing" => BuildDevelopingEmbed(update),
        "reviewing" => BuildReviewingEmbed(update),
        "completed" => BuildCompletedEmbed(update),
        "failed" => BuildFailedEmbed(update),
        _ => BuildGenericEmbed(update)
    };

    await channel.SendMessageAsync(embed: embed);
    return Ok();
}
```

### Discord Embed Examples

```
┌────────────────────────────────────────────────┐
│ 🔄 Working on Issue #25                        │
├────────────────────────────────────────────────┤
│ **Phase:** Analyzing                           │
│ **Agent:** Project Manager                     │
│ **Status:** Exploring codebase...              │
│                                                │
│ ░░░░░░░░░░░░░░░░░░░░ 15%                      │
└────────────────────────────────────────────────┘

┌────────────────────────────────────────────────┐
│ ✅ Issue #25 Completed                         │
├────────────────────────────────────────────────┤
│ **PR Created:** #42                            │
│ **Title:** Fix null reference in UserService   │
│ **Files Changed:** 3                           │
│ **Duration:** 12 minutes                       │
│                                                │
│ [View PR](https://github.com/org/repo/pull/42) │
└────────────────────────────────────────────────┘
```

---

## Workflow Modifications

### Dynamic Agent Roster

Currently, the agent roster is static in configuration. For PM-driven workflows, we need dynamic roster based on PM's recommendation:

```csharp
public interface IDynamicRosterBuilder
{
    AgentRoster BuildFromSpec(IssueSpec spec);
}

public class DynamicRosterBuilder : IDynamicRosterBuilder
{
    public AgentRoster BuildFromSpec(IssueSpec spec)
    {
        var agents = new List<AgentDefinition>
        {
            // PM always runs first (already completed at this point)
        };

        switch (spec.RecommendedWorkflow)
        {
            case WorkflowType.Full:
                agents.AddRange([Architect, Designer, Developer, Reviewer, Tester]);
                break;
            case WorkflowType.BackendOnly:
                agents.AddRange([Architect, Developer, Reviewer, Tester]);
                break;
            case WorkflowType.SimpleFix:
                agents.AddRange([Developer, Reviewer]);
                break;
            case WorkflowType.Trivial:
                agents.Add(Developer);
                break;
        }

        return new AgentRoster(agents);
    }
}
```

### Two-Phase Execution

1. **Phase 1: Analysis** - PM agent runs alone
2. **Phase 2: Implementation** - Remaining agents run based on PM's spec

```csharp
// Modified SupervisorService

private async Task ExecuteProjectAsync(CancellationToken ct)
{
    // Phase 1: Run PM agent
    await SpawnAndWaitForAgentAsync("project-manager", ct);

    // Parse PM's output spec
    var spec = await _specParser.ParseSpecAsync(projectState.WorkingDirectory);

    // Phase 2: Build dynamic roster and continue
    var roster = _rosterBuilder.BuildFromSpec(spec);
    await ExecuteRosterAsync(roster, ct);
}
```

### Spec Parser

```csharp
public interface ISpecParser
{
    Task<IssueSpec> ParseSpecAsync(string workingDirectory);
}

public record IssueSpec
{
    public required int IssueNumber { get; init; }
    public required string Title { get; init; }
    public required WorkflowType RecommendedWorkflow { get; init; }
    public required ScopeSize Scope { get; init; }
    public required IReadOnlyList<string> AffectedFiles { get; init; }
    public string? ImplementationApproach { get; init; }
}

public enum WorkflowType { Full, BackendOnly, SimpleFix, Trivial }
public enum ScopeSize { Trivial, Small, Medium, Large }
```

---

## Configuration Changes

### New Configuration Section

```json
{
  "Apmas": {
    "Discord": {
      "Enabled": true,
      "WebhookCallbackUrl": "https://bot.example.com/webhook/apmas",
      "NotifyOnPhaseChange": true,
      "NotifyOnAgentChange": true,
      "NotifyOnCompletion": true,
      "NotifyOnError": true
    },
    "GitHub": {
      "Repository": "owner/repo",
      "DefaultBranch": "main",
      "CreatePullRequest": true,
      "AutoMergeOnTrivial": false,
      "IssueCommentOnStart": true,
      "IssueCommentOnComplete": true
    },
    "ProjectManager": {
      "Enabled": true,
      "AllowWorkflowOverride": true,
      "DefaultWorkflow": "Auto",
      "SpecOutputPath": "docs/specs"
    }
  }
}
```

### Options Classes

```csharp
public class DiscordIntegrationOptions
{
    public bool Enabled { get; set; }
    public string? WebhookCallbackUrl { get; set; }
    public bool NotifyOnPhaseChange { get; set; } = true;
    public bool NotifyOnAgentChange { get; set; } = true;
    public bool NotifyOnCompletion { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
}

public class GitHubIntegrationOptions
{
    public string? Repository { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public bool CreatePullRequest { get; set; } = true;
    public bool AutoMergeOnTrivial { get; set; } = false;
    public bool IssueCommentOnStart { get; set; } = true;
    public bool IssueCommentOnComplete { get; set; } = true;
}

public class ProjectManagerOptions
{
    public bool Enabled { get; set; } = true;
    public bool AllowWorkflowOverride { get; set; } = true;
    public string DefaultWorkflow { get; set; } = "Auto";
    public string SpecOutputPath { get; set; } = "docs/specs";
}
```

---

## Implementation Phases

### Phase 1: Project Manager Agent (MVP)

**Goal:** PM agent can analyze issues and create specs

- [ ] Create `ProjectManagerPrompt.cs`
- [ ] Register in `PromptFactory`
- [ ] Add to agent roster configuration
- [ ] Test with manual APMAS invocation
- [ ] Validate spec output format

**Deliverables:**
- Working PM agent that creates `docs/specs/issue-{N}-spec.md`
- Manual trigger via CLI: `dotnet run -- --issue 25`

### Phase 2: Dynamic Workflow

**Goal:** PM's spec drives agent selection

- [ ] Create `ISpecParser` and implementation
- [ ] Create `IDynamicRosterBuilder` and implementation
- [ ] Modify `SupervisorService` for two-phase execution
- [ ] Test all workflow paths (Full, BackendOnly, SimpleFix, Trivial)

**Deliverables:**
- Automated workflow selection based on PM analysis
- Different agent chains for different scope sizes

### Phase 3: External API

**Goal:** APMAS can be triggered via HTTP

- [ ] Add ASP.NET Core minimal API
- [ ] Implement `ProjectController`
- [ ] Add webhook callback support
- [ ] Implement `IExternalNotifier`

**Deliverables:**
- `POST /api/project` to start workflow
- `GET /api/project/{id}/status` for polling
- Webhook callbacks on status changes

### Phase 4: Discord Bot Integration

**Goal:** Full Discord → APMAS → Discord flow

- [ ] Add APMAS integration to existing Discord bot
- [ ] Implement command parsing (`@bot fix #25`)
- [ ] Implement webhook handler for status updates
- [ ] Create Discord embed templates
- [ ] Add status/cancel/list commands

**Deliverables:**
- Working Discord bot commands
- Real-time status updates in Discord
- PR link posted on completion

### Phase 5: GitHub Integration

**Goal:** Agents interact with GitHub directly

- [ ] PM agent comments on issue when starting
- [ ] Developer creates branch and commits
- [ ] Reviewer/completion creates PR
- [ ] PR auto-linked to issue (`Closes #25`)
- [ ] Optional: Auto-merge for trivial fixes

**Deliverables:**
- Full GitHub workflow automation
- Issue → PR lifecycle managed by APMAS

---

## Open Questions

### 1. Multiple Concurrent Projects

**Question:** Should APMAS support multiple concurrent projects (issues)?

**Options:**
- A: One at a time (simple, current architecture)
- B: Queue system (first-in-first-out)
- C: True concurrency (complex, resource intensive)

**Recommendation:** Start with B (queue), evolve to C if needed.

---

### 2. Human-in-the-Loop Checkpoints

**Question:** Should there be approval gates before certain phases?

**Examples:**
- PM creates spec → Human approves before Architect starts
- Developer completes → Human reviews before PR creation

**Options:**
- A: Fully autonomous (current design)
- B: Optional approval gates via Discord reactions
- C: Always require approval for Large scope

**Recommendation:** B - Add optional `--approve` flag that pauses for Discord reaction.

---

### 3. Error Recovery UX

**Question:** How should errors be surfaced in Discord?

**Options:**
- A: Simple error message with link to logs
- B: Detailed error embed with stack trace snippet
- C: Interactive retry buttons

**Recommendation:** B with option to C. Include "Retry" and "Cancel" buttons.

---

### 4. Cost/Usage Tracking

**Question:** Should we track token usage per issue?

**Use cases:**
- Budget management
- Cost attribution
- Optimization insights

**Recommendation:** Yes - Add token tracking per agent, report in completion embed.

---

### 5. Spec Format: Markdown vs Structured

**Question:** Should PM output structured data (JSON/YAML) instead of Markdown?

**Tradeoffs:**
- Markdown: Human-readable, easy to review, but parsing is fragile
- Structured: Machine-parseable, reliable, but harder to review

**Recommendation:** Hybrid - Markdown with YAML frontmatter:

```markdown
---
issue: 25
scope: medium
workflow: backend-only
affected_files:
  - src/Services/UserService.cs
  - tests/Services/UserServiceTests.cs
---

# Issue #25: Fix null reference in UserService

## Summary
...
```

---

### 6. Branch Naming Strategy

**Question:** How should branches be named for automated work?

**Options:**
- A: `fix/issue-25`
- B: `apmas/issue-25-fix-null-reference`
- C: `bot/25-user-service-fix`

**Recommendation:** B - Clear provenance, descriptive, sortable.

---

### 7. Existing PR Handling

**Question:** What if a PR already exists for the issue?

**Options:**
- A: Fail with error
- B: Update existing PR
- C: Create new PR, link both

**Recommendation:** A for MVP, evolve to B.

---

## Appendix: File Structure After Implementation

```
src/Apmas.Server/
├── Agents/
│   └── Prompts/
│       └── ProjectManagerPrompt.cs    # NEW
├── Api/                               # NEW
│   ├── ProjectController.cs
│   └── Models/
│       ├── StartProjectRequest.cs
│       └── ProjectStatusUpdate.cs
├── Configuration/
│   ├── DiscordIntegrationOptions.cs   # NEW
│   ├── GitHubIntegrationOptions.cs    # NEW
│   └── ProjectManagerOptions.cs       # NEW
├── Core/
│   └── Services/
│       ├── DynamicRosterBuilder.cs    # NEW
│       ├── SpecParser.cs              # NEW
│       └── ExternalNotifier.cs        # NEW
└── ...

docs/
└── specs/                             # NEW - PM output location
    └── issue-25-spec.md
```

---

*Draft Version: 0.1*
*Last Updated: 2026-01-22*
