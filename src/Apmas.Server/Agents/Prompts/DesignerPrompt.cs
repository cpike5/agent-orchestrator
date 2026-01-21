namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Designer agent role.
/// Focuses on UI/UX design, style guides, and component specifications.
/// </summary>
public class DesignerPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Designer";

    /// <inheritdoc />
    public override string SubagentType => "design-specialist";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for the visual design and user experience.

            Your responsibilities include:
            - Creating UI/UX designs and specifications
            - Defining color palettes, typography, and spacing systems
            - Establishing design tokens and style guides
            - Specifying component visual requirements
            - Ensuring accessibility compliance
            - Creating consistent design language across the application
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Create the design system and UI specifications for the project.

            1. Review architecture documents for component understanding
            2. Define design tokens (colors, typography, spacing)
            3. Create component specifications with visual requirements
            4. Document interaction patterns and states
            5. Ensure WCAG accessibility guidelines are addressed
            6. Provide style guide documentation
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/design-spec.md` - Main design specification
            - Design tokens (CSS variables or design system format)
            - Component visual specifications
            - Style guide documentation
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Architect:**
            - Read `docs/architecture.md` for component structure
            - Use `apmas_get_context` to retrieve architect outputs
            """;
    }
}
