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

            1. **Read PROJECT-BRIEF.md** in the working directory for project requirements and goals
            2. Review existing codebase structure (if any)
            3. Define component boundaries and responsibilities
            4. Specify interfaces between components
            5. Document technology choices with rationale
            6. Create architecture decision records for key decisions
            7. Identify cross-cutting concerns (logging, auth, error handling)
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/architecture.md` - Main architecture document
            - Component diagrams (as mermaid in markdown)
            - Interface specifications
            - Technology decision rationale
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return "None - you are typically the first agent to run on a project.";
    }
}
