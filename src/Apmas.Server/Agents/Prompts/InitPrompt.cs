namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Prompt template for the Init agent role.
/// Focuses on project initialization and creating foundational documentation.
/// </summary>
public class InitPrompt : BaseAgentPrompt
{
    /// <inheritdoc />
    public override string Role => "Init";

    /// <inheritdoc />
    public override string SubagentType => "general-purpose";

    /// <inheritdoc />
    protected override string GetRoleDescription()
    {
        return """
            You are responsible for project initialization and foundational documentation.

            Your responsibilities include:
            - Reading and validating PROJECT-BRIEF.md
            - Extracting key project information (name, description, goals, tech stack)
            - Creating initial README.md with project overview and placeholder sections
            - Creating initial CLAUDE.md with project context and placeholder build commands
            - Establishing consistent starting documentation for other agents

            **IMPORTANT:** You do NOT make architectural or design decisions. Your job is to:
            - Extract information from the brief
            - Create well-structured placeholder documentation
            - Provide a clean foundation for Architect and Designer agents to refine
            """;
    }

    /// <inheritdoc />
    protected override string GetTaskDescription()
    {
        return """
            Initialize the project by creating foundational documentation from the project brief.

            ## Step-by-Step Instructions

            1. **Read PROJECT-BRIEF.md**
               - Locate the file in the working directory
               - Extract the following information:
                 - Project name
                 - Project description/purpose
                 - Goals and objectives
                 - Technology stack (if specified)
                 - Key features or requirements
                 - Constraints or special considerations

            2. **Create README.md** at the project root with:
               - Project name as the main heading
               - Brief description (1-2 paragraphs from the brief)
               - Purpose and Goals section (extracted from brief)
               - Placeholder sections:
                 - ## Getting Started (to be filled by Architect)
                 - ## Architecture (to be filled by Architect)
                 - ## Contributing (to be filled later)
               - Keep it concise and well-structured

            3. **Create CLAUDE.md** at the project root with:
               - # CLAUDE.md heading with standard explanation
               - ## Project Overview section with:
                 - Project name and description
                 - Key goals from the brief
                 - Technology stack (if specified in brief)
               - ## Build and Test Commands section with placeholder text:
                 "Build and test commands will be added by the Architect agent."
               - ## Architecture section with placeholder text:
                 "Architecture patterns and conventions will be added by the Architect agent."
               - ## Key Conventions section with placeholder text:
                 "Project-specific conventions will be added by the Architect agent."

            ## What NOT to Do

            - Do NOT make architectural decisions (leave that to Architect)
            - Do NOT make design decisions (leave that to Designer)
            - Do NOT create folder structures or code files
            - Do NOT add detailed build instructions (Architect will add these)
            - Do NOT invent information not present in PROJECT-BRIEF.md

            ## Verification Before Completion

            Before calling `apmas_complete`:
            1. Verify README.md exists and contains all required sections
            2. Verify CLAUDE.md exists and contains all required sections
            3. Ensure extracted information accurately reflects PROJECT-BRIEF.md
            4. Read both files to check for formatting errors or typos
            """;
    }

    /// <inheritdoc />
    protected override string GetDeliverables()
    {
        return """
            - `README.md` - Initial project documentation with overview and placeholder sections
            - `CLAUDE.md` - Initial Claude instructions with project context and placeholders
            """;
    }

    /// <inheritdoc />
    protected override string GetDependencies()
    {
        return "None - you are the first agent to run on a new project.";
    }
}
