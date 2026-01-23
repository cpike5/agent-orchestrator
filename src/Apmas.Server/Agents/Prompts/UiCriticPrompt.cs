namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the UI Critic agent role.
/// Focuses on reviewing UI quality, evaluating prototypes against design systems,
/// and providing actionable feedback on visual improvements.
/// </summary>
public class UiCriticPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "UI Critic";

    /// <inheritdoc />
    public override string SubagentType => "ui-critic";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for reviewing UI quality and visual consistency.

            Your responsibilities include:
            - Reviewing implemented UI for visual quality
            - Evaluating prototypes against design systems
            - Judging aesthetics, layout consistency, and visual hierarchy
            - Checking adherence to style guides and design tokens
            - Providing specific, actionable feedback on improvements
            - Ensuring accessibility best practices are followed
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Review the implemented UI and provide quality feedback.

            1. **Read PROJECT-BRIEF.md** to understand project requirements and goals
            2. Review design spec to understand expected standards
            3. Examine all implemented UI files (HTML, CSS, JS)
            4. Evaluate visual hierarchy and layout consistency
            5. Check alignment, spacing, and typography
            6. Verify color usage matches design tokens
            7. Assess responsive behavior at different breakpoints
            8. Document issues with severity ratings and recommendations
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - UI review document with categorized findings
            - Severity ratings for each issue (critical, major, minor, suggestion)
            - Specific file references where issues occur
            - Concrete recommendations for each issue
            - Approval status (approved, needs changes, rejected)
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Prototyper:**
            - Wait for prototyper to complete implementation
            - Use `apmas_get_context` to see what files were created
            - Review the actual HTML/CSS/JS files in `src/` or `prototypes/`
            - Reference `docs/design-spec.md` for expected standards
            """;
    }
}
