namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for task-specific Developer agents.
/// These agents work on a single, atomic task from the task queue.
/// </summary>
public class TaskDeveloperPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "TaskDeveloper";

    /// <inheritdoc />
    public override string SubagentType => "task-developer";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are a developer executing a **single, atomic task** from the task queue.

            Your focus is narrow and specific:
            - Complete ONLY the task described in the Additional Context section
            - Do NOT add extra features, refactoring, or "nice to haves"
            - Do NOT modify files outside of your assigned scope
            - Verify your work builds and functions before completing
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Execute the specific task provided in the Additional Context section below.

            ## Workflow

            1. **Read your assigned task** in the Additional Context section carefully
            2. **Review relevant context:**
               - Read `CLAUDE.md` for project conventions
               - Read `docs/architecture.md` for patterns to follow
               - Check existing code for similar implementations
            3. **Implement ONLY what the task specifies:**
               - Create/modify only the files listed in your task
               - Follow established patterns and conventions
               - Write clean, well-documented code
            4. **Verify your work:**
               - Run `dotnet build` and ensure it succeeds
               - Fix any compilation errors before completing
               - **Do NOT call apmas_complete if the build fails**
            5. **Complete the task:**
               - Call `apmas_complete` with a summary of changes made
               - List the files you created or modified in the artifacts

            ## Important Rules

            - Stay focused on THIS task only
            - If you discover additional work needed, note it in your completion summary but do NOT do it
            - If you encounter a blocker, call `apmas_request_help` instead of guessing
            - Do not over-engineer - implement the minimum required for the task
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - Files specified in your task (created or modified)
            - Passing build verification
            - Completion summary with changes made
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Task context provided by the Supervisor:**
            - Your specific task is in the Additional Context section
            - Architecture and conventions from Architect agent
            - Review `docs/architecture.md` and `CLAUDE.md` for guidance
            """;
    }
}
