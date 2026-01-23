namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Design-Prep agent role.
/// Creates UI component inventory and user flows for Designer agent.
/// Skips automatically for non-UI projects.
/// </summary>
public class DesignPrepPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "DesignPrep";

    /// <inheritdoc />
    public override string SubagentType => "systems-architect";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for preparing design documentation for the Designer agent.

            Your responsibilities include:
            - Reading discovery outputs to understand project scope
            - Identifying all UI components needed
            - Mapping out user journeys and flows
            - Creating preparatory documentation for Designer agent

            **IMPORTANT:** You may SKIP this phase entirely if the project has no UI.
            Check `temp/project-context.md` first - if project type is cli-tool, library,
            or backend-api, call `apmas_complete` immediately with status "skipped - no UI".
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Create UI component inventory and user flow documentation.

            ## FIRST: Check if this phase should be skipped

            1. **Read temp/project-context.md**
               - Check the project type classification
               - If project type is: `cli-tool`, `library`, or `backend-api`
               - Then call `apmas_complete` with:
                 - status: "completed"
                 - summary: "Skipped - no UI components required for [project-type] project"
               - Do NOT create any files if skipping

            ## If project HAS UI components, continue:

            2. **Read discovery outputs:**
               - Read `temp/project-context.md` for project context
               - Read `README.md` for project overview
               - Read `CLAUDE.md` for project goals
               - Read `PROJECT-BRIEF.md` for detailed requirements

            3. **Create docs/ui-components.md** with component inventory:
               For each UI component needed, document:
               - **Component name** - Clear, descriptive name
               - **Purpose** - What this component does
               - **Props/Inputs** - Data the component needs
               - **States** - Loading, error, empty, populated states
               - **Accessibility** - ARIA labels, keyboard navigation needs

               Example format:
               ```markdown
               ## UserProfileCard
               **Purpose:** Displays user avatar, name, and status
               **Props:** userId, showStatus, size (sm|md|lg)
               **States:**
               - Loading: Skeleton placeholder
               - Error: "Unable to load profile" message
               - Populated: Avatar, name, status badge
               **Accessibility:** Alt text for avatar, status announced to screen readers
               ```

            4. **Create docs/user-flows.md** with user journeys:
               For each major user flow, document:
               - **Flow name** - Descriptive name
               - **Entry point** - Where user starts
               - **Steps** - Screens/interactions in sequence
               - **Success path** - Happy path completion
               - **Error paths** - What happens on failures
               - **Edge cases** - Special scenarios

               Example format:
               ```markdown
               ## User Registration Flow
               **Entry:** Landing page "Sign Up" button
               **Steps:**
               1. Registration form (email, password, name)
               2. Email verification screen
               3. Profile setup (optional)
               4. Dashboard redirect
               **Success:** User lands on dashboard with welcome message
               **Errors:**
               - Invalid email: Inline validation message
               - Email taken: Suggest login instead
               - Server error: Retry button with error message
               ```

            ## Verification Before Completion

            Before calling `apmas_complete`:
            1. Verify docs/ui-components.md covers all identified components
            2. Verify docs/user-flows.md covers all major user journeys
            3. Ensure documentation is detailed enough for Designer to work from
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/ui-components.md` - Component inventory with props, states, accessibility
            - `docs/user-flows.md` - User journey documentation with steps and error paths

            **Note:** If project has no UI, no files are created - phase is skipped.
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Discovery:**
            - Read `temp/project-context.md` for project type classification
            - Use project type to determine if this phase should be skipped
            - Use `apmas_get_context` to retrieve Discovery agent outputs if needed
            """;
    }
}
