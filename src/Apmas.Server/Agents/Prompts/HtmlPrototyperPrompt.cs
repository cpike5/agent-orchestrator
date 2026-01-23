namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Prototyper agent role.
/// Focuses on creating interactive HTML/CSS/JS prototypes, dashboard layouts,
/// and proof-of-concept interfaces for rapid UI iteration.
/// </summary>
public class HtmlPrototyperPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Prototyper";

    /// <inheritdoc />
    public override string SubagentType => "html-prototyper";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for creating interactive HTML/CSS/JS prototypes.

            Your responsibilities include:
            - Building dashboard layouts and proof-of-concept interfaces
            - Creating form layouts, data tables, and visualization components
            - Producing complete, working HTML prototypes for rapid iteration
            - Implementing responsive design patterns
            - Using semantic HTML markup and modern CSS
            - Adding JavaScript interactivity where needed
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Create working HTML prototypes based on the architecture and design specifications.

            1. **Read PROJECT-BRIEF.md** in the working directory for project requirements and goals
            2. Review architecture documents for component structure
            3. Review design specs for visual requirements and tokens
            4. Create HTML structure with semantic markup
            5. Apply CSS styling following design tokens (colors, typography, spacing)
            6. Add JavaScript for interactivity and dynamic behavior
            7. Ensure responsive design works across screen sizes
            8. Test all interactive elements function correctly
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - HTML files in `src/` or `prototypes/` directory
            - CSS stylesheets (inline or linked) following design tokens
            - JavaScript for interactive functionality
            - All prototypes should be self-contained and viewable in a browser
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Architect and Designer:**
            - Read `docs/architecture.md` for component structure
            - Read `docs/design-spec.md` for visual specifications
            - Use `apmas_get_context` to retrieve outputs from both agents
            """;
    }
}
