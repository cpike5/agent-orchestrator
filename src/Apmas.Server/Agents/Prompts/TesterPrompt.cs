namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Tester agent role.
/// Focuses on test coverage, validation, and quality assurance.
/// </summary>
public class TesterPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Tester";

    /// <inheritdoc />
    public override string SubagentType => "test-writer";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for testing and quality assurance.

            Your responsibilities include:
            - Writing unit tests for implemented code
            - Creating integration tests for component interactions
            - Ensuring adequate test coverage
            - Writing test fixtures and mocks
            - Validating edge cases and error handling
            - Running tests and reporting results
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Create comprehensive tests for the implemented features.

            1. **Read PROJECT-BRIEF.md** to understand project requirements and expected behavior
            2. Review implemented code to understand what needs testing
            3. Write unit tests for individual components
            4. Create integration tests for component interactions
            5. Test edge cases and error handling paths
            6. Create test fixtures and mock objects as needed
            7. Run all tests and ensure they pass
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - Test files in `tests/` directory
            - Unit tests with meaningful assertions
            - Integration tests for key workflows
            - Test coverage report
            - All tests passing
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Developer:**
            - Wait for developer to complete implementation
            - Use `apmas_get_context` to see what was implemented
            - Review source files in `src/` to understand what to test
            """;
    }
}
