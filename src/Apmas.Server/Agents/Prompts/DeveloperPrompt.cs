namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Developer agent role.
/// Focuses on implementation, code quality, and following specifications.
/// </summary>
public class DeveloperPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Developer";

    /// <inheritdoc />
    public override string SubagentType => "dotnet-specialist";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for implementing features and writing production code.

            Your responsibilities include:
            - Implementing features according to specifications
            - Following architecture patterns and conventions
            - Writing clean, maintainable code
            - Implementing proper error handling
            - Following security best practices
            - Creating code that matches design specifications
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Implement the specified features following architecture and design specifications.

            1. **Read PROJECT-BRIEF.md** in the working directory for project requirements and goals
            2. Review architecture document for component structure
            3. Review design specifications for UI requirements
            4. Implement features following established patterns
            5. Ensure proper error handling and logging
            6. Write code that is testable and maintainable
            7. Follow security best practices (OWASP guidelines)
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - Source files in `src/` directory
            - Properly structured and documented code
            - Implementation following architecture patterns
            - UI matching design specifications
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Architect and Designer:**
            - Read `docs/architecture.md` for structure and patterns
            - Read `docs/design-spec.md` for UI specifications
            - Use `apmas_get_context` to retrieve their outputs
            - Wait for both to complete before starting implementation
            """;
    }
}
