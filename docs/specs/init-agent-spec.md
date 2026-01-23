# Init Agent Specification

## Overview

The Init agent is a new root agent that runs before Architect and Designer agents. Its purpose is to take a project brief and establish the foundational project documentation (README.md and CLAUDE.md) that subsequent agents will refine.

## Motivation

Currently, the Architect agent is responsible for creating CLAUDE.md and README.md from scratch. This creates two issues:
1. Architect has too many responsibilities (architecture + documentation setup)
2. Designer starts in parallel without consistent project context

By introducing an Init agent, we:
- Separate concerns: Init handles project scaffolding, Architect handles architecture
- Ensure both Architect and Designer start with consistent foundational docs
- Allow Architect/Designer to refine existing docs rather than create from scratch

## Agent Flow Change

### Before
```
Architect ──┐
            ├──→ Developer → Reviewer
Designer ───┘
```

### After
```
Init ──→ Architect ──┐
    └──→ Designer ───┴──→ Developer → ...
```

## Init Agent Responsibilities

1. **Validate PROJECT-BRIEF.md** - Ensure the file exists and contains required sections
2. **Create README.md** - Initial project README with:
   - Project name and description (from brief)
   - Purpose and goals
   - Placeholder sections for: Getting Started, Architecture, Contributing
3. **Create CLAUDE.md** - Project-specific Claude instructions with:
   - Project overview extracted from brief
   - Placeholder build/test commands (to be filled by Architect)
   - Technology stack (if specified in brief)
   - Coding conventions section
4. **Report completion** - Use `apmas_complete` with artifacts list

## Implementation Requirements

### 1. Create InitPrompt.cs

Location: `src/Apmas.Server/Agents/Prompts/InitPrompt.cs`

```csharp
public class InitPrompt : BaseAgentPrompt
{
    public override string Role => "Init";
    public override string SubagentType => "general-purpose";

    // Implement abstract methods...
}
```

Key prompt instructions:
- Read PROJECT-BRIEF.md thoroughly
- Extract: project name, description, goals, tech stack, constraints
- Create README.md with extracted info + placeholder sections
- Create CLAUDE.md with project context + placeholder build commands
- Do NOT make architectural decisions (that's Architect's job)
- Do NOT make design decisions (that's Designer's job)
- Call apmas_heartbeat every 5 minutes
- Call apmas_complete when done with artifact paths

### 2. Register in AgentServiceExtensions.cs

Add to `AddAgentPrompts()`:
```csharp
services.AddSingleton<BaseAgentPrompt, InitPrompt>();
```

### 3. Update appsettings.json

Add init agent to roster (as first entry):
```json
{
  "Role": "init",
  "SubagentType": "general-purpose",
  "Dependencies": [],
  "Description": "Project initialization - creates README.md and CLAUDE.md from project brief"
}
```

Update architect and designer to depend on init:
```json
{
  "Role": "architect",
  "SubagentType": "systems-architect",
  "Dependencies": ["init"],
  ...
}
```
```json
{
  "Role": "designer",
  "SubagentType": "design-specialist",
  "Dependencies": ["init"],
  ...
}
```

### 4. Update ArchitectPrompt.cs

Remove instructions to create CLAUDE.md and README.md from scratch. Instead:
- Instruct to UPDATE/REFINE existing CLAUDE.md with build commands
- Instruct to UPDATE/REFINE existing README.md with architecture section

### 5. Update DesignerPrompt.cs

Add instruction to:
- Read existing README.md and CLAUDE.md for project context
- Update README.md with design-related sections if needed

## Deliverables

The Init agent produces:
- `README.md` - Initial project documentation
- `CLAUDE.md` - Initial Claude instructions

## Success Criteria

1. Init agent runs first (before Architect and Designer)
2. README.md created with project name, description, and placeholder sections
3. CLAUDE.md created with project overview and placeholder build commands
4. Architect and Designer receive consistent starting context
5. No circular dependencies introduced
6. All existing tests pass

## Out of Scope

- Environment validation (future enhancement)
- Tool/dependency checking (future enhancement)
- Folder structure creation beyond docs (Architect's responsibility)
