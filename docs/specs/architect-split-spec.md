# Architect Agent Split Specification

> **Status:** Draft
> **Created:** 2026-01-22

## Summary

Replace the current `init` + `architect` agents with three distinct phases:
1. **discovery** - Project understanding and foundational docs (absorbs init)
2. **design-prep** - UI component inventory and user flows (conditionally skips for non-UI projects)
3. **architect** - Implementation specs and task breakdown for developer

## Motivation

The current architect agent tries to do too much in a single pass:
- Initial project review
- Documentation creation
- Design preparation
- Implementation planning

Splitting into three phases provides:
- **Better separation of concerns** - each phase has a clear, focused responsibility
- **Parallel execution** - architect and design-prep can run simultaneously after discovery
- **Conditional execution** - design-prep can skip for non-UI projects
- **Improved reliability** - smaller scope reduces context limit issues and timeouts

## New Dependency Graph

```
discovery (root - no dependencies)
├── design-prep (depends on discovery)
│   └── designer (depends on design-prep)  ← updated
│       └── prototyper (depends on designer)
└── architect (depends on discovery)  ← can run parallel to design track

developer (depends on architect, designer, prototyper)
```

## Phase Specifications

### Phase 1: Discovery Agent

**Role:** `discovery`
**SubagentType:** `general-purpose`
**Dependencies:** none (root agent)
**Timeout:** 10 minutes

**Responsibilities:**
- Read and validate PROJECT-BRIEF.md
- Review existing codebase structure (if any)
- Identify project type (UI app, CLI tool, library, backend API, etc.)
- Extract key project information (name, description, goals, tech stack)
- Gather basic requirements and constraints

**Deliverables:**
- `README.md` - Initial project documentation with placeholder sections
- `CLAUDE.md` - Initial Claude instructions with project context
- `temp/project-context.md` - Gathered context including:
  - Project type classification
  - Existing file structure summary
  - Technology stack identified
  - Key requirements extracted
  - Constraints and considerations

**What NOT to do:**
- Do NOT make architectural decisions
- Do NOT make design decisions
- Do NOT create implementation specs
- Do NOT create folder structures or code files

### Phase 2: Design-Prep Agent

**Role:** `design-prep`
**SubagentType:** `systems-architect`
**Dependencies:** `["discovery"]`
**Timeout:** 10 minutes

**Skip Logic:**
Check `temp/project-context.md` for project type. If project is classified as:
- CLI tool
- Library
- Backend-only API
- Any non-UI project

Then call `apmas_complete` immediately with status: "skipped - no UI components required"

**Responsibilities (when UI exists):**
- Read discovery outputs to understand project scope
- Identify all UI components needed
- Map out user journeys and flows
- Create preparatory documentation for Designer agent

**Deliverables:**
- `docs/ui-components.md` - Component inventory including:
  - Component name
  - Purpose/description
  - Props/inputs expected
  - States (loading, error, empty, populated)
  - Accessibility considerations
- `docs/user-flows.md` - User journey descriptions including:
  - Flow name
  - Entry point
  - Steps/screens involved
  - Success/error paths
  - Edge cases

### Phase 3: Architect Agent (Updated)

**Role:** `architect`
**SubagentType:** `systems-architect`
**Dependencies:** `["discovery"]`
**Timeout:** 15 minutes

**Responsibilities:**
- Read discovery outputs (README, CLAUDE.md, temp/project-context.md)
- Define system architecture and component boundaries
- Make technology stack decisions
- Establish patterns and conventions
- Create implementation specifications
- Decompose work into atomic tasks

**Deliverables:**
- `docs/architecture.md` - Main architecture document
- Updated `CLAUDE.md` with:
  - Build and test commands
  - Architecture patterns and conventions
  - Important file locations
- Updated `README.md` with:
  - Architecture section
  - Getting Started section
- Task breakdown submitted via `apmas_submit_tasks` tool

### Designer Agent (Updated Dependencies)

**Dependencies:** `["design-prep"]` (changed from `["init"]`)

**Updated Task Description:**
- Read `docs/ui-components.md` from design-prep
- Read `docs/user-flows.md` from design-prep
- Create design specifications building on the component inventory

## Files to Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Apmas.Server/Agents/Prompts/DiscoveryPrompt.cs` | Create | New discovery phase prompt |
| `src/Apmas.Server/Agents/Prompts/DesignPrepPrompt.cs` | Create | New design-prep phase prompt |
| `src/Apmas.Server/Agents/Prompts/ArchitectPrompt.cs` | Modify | Update for new flow |
| `src/Apmas.Server/Agents/Prompts/DesignerPrompt.cs` | Modify | Update dependencies |
| `src/Apmas.Server/Agents/Prompts/InitPrompt.cs` | Delete | Absorbed by discovery |
| `src/Apmas.Server/Agents/AgentServiceExtensions.cs` | Modify | Update DI registration |
| `src/Apmas.Server/appsettings.json` | Modify | Update roster configuration |

## Configuration Changes

### appsettings.json Roster

```json
{
  "Agents": {
    "Roster": [
      {
        "Role": "discovery",
        "SubagentType": "general-purpose",
        "Dependencies": [],
        "Description": "Project discovery - reviews brief, creates README/CLAUDE.md, gathers context",
        "PromptType": "DiscoveryPrompt"
      },
      {
        "Role": "design-prep",
        "SubagentType": "systems-architect",
        "Dependencies": ["discovery"],
        "Description": "Design preparation - creates UI component inventory and user flows (skips for non-UI)",
        "PromptType": "DesignPrepPrompt"
      },
      {
        "Role": "architect",
        "SubagentType": "systems-architect",
        "Dependencies": ["discovery"],
        "Description": "Implementation specs and task breakdown",
        "PromptType": "ArchitectPrompt"
      },
      {
        "Role": "designer",
        "SubagentType": "design-specialist",
        "Dependencies": ["design-prep"],
        "Description": "UI/UX design and design systems",
        "PromptType": "DesignerPrompt"
      }
    ]
  }
}
```

### Timeout Overrides

```json
{
  "Timeouts": {
    "AgentOverrides": {
      "discovery": 10,
      "design-prep": 10,
      "architect": 15,
      "designer": 15,
      "developer": 45,
      "reviewer": 20
    }
  }
}
```

## Implementation Order

1. Create `DiscoveryPrompt.cs` (new file)
2. Create `DesignPrepPrompt.cs` (new file)
3. Update `ArchitectPrompt.cs` (modify existing)
4. Update `DesignerPrompt.cs` (modify existing)
5. Update `AgentServiceExtensions.cs` (DI registration)
6. Update `appsettings.json` (roster configuration)
7. Delete `InitPrompt.cs` (cleanup)

## Verification

1. **Build:** Run `dotnet build` to ensure no compilation errors
2. **Unit tests:** Run `dotnet test` to verify existing tests pass
3. **Manual test:** Start the server and verify agents spawn in correct order:
   - discovery runs first
   - design-prep and architect run in parallel after discovery
   - designer waits for design-prep
   - developer waits for architect, designer, and prototyper
4. **Non-UI test:** Run with a CLI project brief to verify design-prep skips correctly
