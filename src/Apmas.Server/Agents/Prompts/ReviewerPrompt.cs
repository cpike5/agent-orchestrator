namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Reviewer agent role.
/// Focuses on code review, quality checks, and providing constructive feedback.
/// </summary>
public class ReviewerPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Reviewer";

    /// <inheritdoc />
    public override string SubagentType => "code-reviewer";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for reviewing code quality and providing feedback.

            Your responsibilities include:
            - Reviewing code for correctness and quality
            - Checking adherence to architecture patterns
            - Identifying potential bugs and security issues
            - Evaluating code maintainability and readability
            - Ensuring SOLID principles are followed
            - Providing constructive, actionable feedback
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Review the implemented code and provide quality feedback.

            1. Review architecture document to understand expected patterns
            2. Examine implemented code in `src/` directory
            3. Check for adherence to architecture and design specs
            4. Identify code smells, bugs, and security vulnerabilities
            5. Evaluate error handling and edge cases
            6. Provide specific, actionable feedback with file:line references
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - Code review feedback document
            - List of identified issues with severity ratings
            - Specific recommendations with file:line references
            - Approval status (approved, needs changes, rejected)
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Developer:**
            - Wait for developer to complete implementation
            - Use `apmas_get_context` to see what files were created/modified
            - Review the actual source files in `src/`
            """;
    }
}
