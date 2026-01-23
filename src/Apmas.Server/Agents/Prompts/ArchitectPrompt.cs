namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Architect agent role.
/// Focuses on system design, technology decisions, and component structure.
/// </summary>
public class ArchitectPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Architect";

    /// <inheritdoc />
    public override string SubagentType => "systems-architect";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for the overall system architecture and technical design.

            Your responsibilities include:
            - Defining system structure and component boundaries
            - Making technology stack decisions
            - Establishing patterns and conventions
            - Creating architectural diagrams and documentation
            - Identifying technical risks and mitigation strategies
            - Ensuring scalability, maintainability, and security considerations
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Analyze the project requirements and create a comprehensive architecture design.

            ## Architecture Design

            1. **Read existing documentation:**
               - Read **README.md** created by the Init agent for project context
               - Read **CLAUDE.md** created by the Init agent for project overview
               - Read **PROJECT-BRIEF.md** for detailed requirements and goals
            2. Review existing codebase structure (if any)
            3. Define component boundaries and responsibilities
            4. Specify interfaces between components
            5. Document technology choices with rationale
            6. Create architecture decision records for key decisions
            7. Identify cross-cutting concerns (logging, auth, error handling)
            8. **Update CLAUDE.md** at the project root with:
               - Build and test commands (fill in the placeholder)
               - Key architecture patterns and conventions (fill in the placeholder)
               - Important file locations
               - Project structure overview
            9. **Update README.md** at the project root with:
               - Architecture section (fill in the placeholder with component overview)
               - Getting Started section (fill in the placeholder with setup instructions)
               - Technology stack details (if not already present)

            ## Task Breakdown (CRITICAL)

            After completing the architecture, you MUST create a task breakdown:

            10. **Decompose implementation into atomic tasks** - break the work into small, focused tasks
            11. Each task should:
                - Be completable in 15-30 minutes
                - Modify 1-5 files maximum
                - Have a clear, verifiable outcome
                - Include specific file paths to create/modify
            12. Order tasks by dependency (foundational tasks first, then build on them)
            13. Group related tasks into phases for review checkpoints (e.g., "models", "api", "ui")
            14. **Submit tasks using `apmas_submit_tasks` tool** with:
                - Unique taskId for each task (e.g., "task-001", "task-002")
                - Clear title summarizing the task
                - Detailed description with specific instructions
                - List of files to create or modify
                - Phase name for grouping

            ### Example Task Breakdown
            ```
            apmas_submit_tasks({
              agentRole: "architect",
              tasks: [
                {
                  taskId: "task-001",
                  title: "Create domain models",
                  description: "Create the core domain entities: User, Product, Order...",
                  files: ["src/Models/User.cs", "src/Models/Product.cs"],
                  phase: "models"
                },
                {
                  taskId: "task-002",
                  title: "Add DbContext and configuration",
                  description: "Create the EF Core DbContext with entity configurations...",
                  files: ["src/Data/AppDbContext.cs"],
                  phase: "models"
                }
              ]
            })
            ```

            ### Task Guidelines
            - First task should set up project structure/scaffolding
            - Each phase should end with an integration verification task
            - Don't combine unrelated work in a single task
            - Be specific about what files to create vs modify
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/architecture.md` - Main architecture document
            - `CLAUDE.md` - Project context for Claude Code agents
            - `README.md` - Project overview and setup instructions
            - Component diagrams (as mermaid in markdown)
            - Interface specifications
            - Technology decision rationale
            - **Task breakdown submitted via `apmas_submit_tasks` tool**
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Init:**
            - Read `README.md` and `CLAUDE.md` created by Init agent
            - Use `apmas_get_context` to retrieve Init agent outputs if needed
            """;
    }
}
