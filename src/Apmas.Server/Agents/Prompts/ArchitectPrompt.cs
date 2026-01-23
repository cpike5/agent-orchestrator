namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Architect agent role.
/// Focuses on system design, technology decisions, and task breakdown.
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
            - Creating architectural documentation
            - Decomposing work into atomic tasks for developers
            - Ensuring scalability, maintainability, and security considerations
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Create architecture design and decompose implementation into tasks.

            ## Step 1: Read Discovery Outputs

            1. **Read temp/project-context.md** for:
               - Project type classification
               - Technology stack identified
               - Key requirements and constraints
            2. **Read README.md** for project overview
            3. **Read CLAUDE.md** for project goals
            4. **Read PROJECT-BRIEF.md** for detailed requirements

            ## Step 2: Architecture Design

            5. **Define component boundaries** and responsibilities
            6. **Specify interfaces** between components
            7. **Document technology choices** with rationale
            8. **Identify cross-cutting concerns** (logging, auth, error handling)

            ## Step 3: Create Architecture Documentation

            9. **Create docs/architecture.md** with:
               - System overview diagram (mermaid)
               - Component descriptions and responsibilities
               - Interface specifications
               - Technology decisions with rationale
               - Data flow descriptions

            10. **Update CLAUDE.md** with:
                - Build and test commands (fill in placeholder)
                - Architecture patterns and conventions
                - Important file locations
                - Project structure overview

            11. **Update README.md** with:
                - Architecture section (component overview)
                - Getting Started section (setup instructions)

            ## Step 4: Task Breakdown (CRITICAL)

            12. **Decompose implementation into atomic tasks**
                Each task should:
                - Be completable in 15-30 minutes
                - Modify 1-5 files maximum
                - Have a clear, verifiable outcome
                - Include specific file paths to create/modify

            13. **Order tasks by dependency** (foundational tasks first)
            14. **Group related tasks into phases** (e.g., "models", "api", "ui")

            15. **Submit tasks using `apmas_submit_tasks` tool**:
                ```
                apmas_submit_tasks({
                  agentRole: "architect",
                  tasks: [
                    {
                      taskId: "task-001",
                      title: "Create domain models",
                      description: "Create core entities: User, Product...",
                      files: ["src/Models/User.cs", "src/Models/Product.cs"],
                      phase: "models"
                    }
                  ]
                })
                ```

            ### Task Guidelines
            - First task should set up project structure/scaffolding
            - Each phase should end with verification task
            - Don't combine unrelated work in a single task
            - Be specific about files to create vs modify
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/architecture.md` - Main architecture document
            - Updated `CLAUDE.md` - Build commands, patterns, file locations
            - Updated `README.md` - Architecture and Getting Started sections
            - **Task breakdown submitted via `apmas_submit_tasks` tool**
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Discovery:**
            - Read `temp/project-context.md` for project classification
            - Read `README.md` and `CLAUDE.md` created by Discovery
            - Use `apmas_get_context` to retrieve Discovery outputs if needed
            """;
    }
}
