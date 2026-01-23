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

            ## Step 1: Read Design-Prep Outputs

            1. **Read docs/ui-components.md** for:
               - Component inventory with props and states
               - Accessibility requirements per component
            2. **Read docs/user-flows.md** for:
               - User journey documentation
               - Screen sequences and interactions
            3. **Read temp/project-context.md** for project context
            4. **Read PROJECT-BRIEF.md** for any design preferences mentioned

            ## Step 2: Design System Creation

            5. **Define design tokens:**
               - Color palette (primary, secondary, semantic colors)
               - Typography scale (font families, sizes, weights)
               - Spacing system (consistent spacing values)
               - Border radii, shadows, transitions

            6. **Create component specifications:**
               - Visual design for each component in ui-components.md
               - State variations (hover, focus, active, disabled)
               - Responsive behavior
               - Animation/transition specifications

            7. **Document interaction patterns:**
               - Button interactions
               - Form input behaviors
               - Navigation patterns
               - Loading and error states

            8. **Ensure WCAG accessibility:**
               - Color contrast ratios
               - Focus indicators
               - Touch target sizes

            ## Step 3: Deliverables

            9. **Create docs/design-spec.md** with:
               - Design tokens (as CSS variables)
               - Component visual specifications
               - Interaction patterns
               - Accessibility guidelines

            10. **Create docs/style-guide.md** with:
                - Usage examples for design tokens
                - Do's and don'ts
                - Component usage guidelines
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `docs/design-spec.md` - Main design specification with tokens and components
            - `docs/style-guide.md` - Usage guidelines and examples
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return """
            **Depends on Design-Prep:**
            - Read `docs/ui-components.md` for component inventory
            - Read `docs/user-flows.md` for user journey context
            - Use `apmas_get_context` to retrieve Design-Prep outputs if needed
            """;
    }
}
