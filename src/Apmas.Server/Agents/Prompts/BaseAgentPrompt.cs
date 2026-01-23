using Apmas.Server.Core.Models;

namespace Apmas.Server.Agents.Prompts;

/// <summary>
/// Base prompt template that all agent prompts extend.
/// Provides common structure for APMAS communication instructions.
/// </summary>
public abstract class BaseAgentPrompt
{
    /// <summary>
    /// The role name for this agent (e.g., "Developer", "Architect").
    /// </summary>
    public abstract string Role { get; }

    /// <summary>
    /// The Claude Code subagent type to use (e.g., "dotnet-specialist", "systems-architect").
    /// </summary>
    public abstract string SubagentType { get; }

    /// <summary>
    /// Generates the complete prompt for the agent.
    /// This method is called by the AgentSpawner when launching an agent.
    /// The generated prompt includes all APMAS communication instructions
    /// and can be augmented with additional context (e.g., checkpoint data
    /// for agent restart scenarios).
    /// </summary>
    /// <param name="project">The current project state.</param>
    /// <param name="additionalContext">Optional additional context to inject into the prompt.
    /// Empty or whitespace-only strings are treated as no context.</param>
    /// <returns>The complete prompt string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="project"/> is null.</exception>
    public string Generate(ProjectState project, string? additionalContext = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        var additionalSection = string.IsNullOrWhiteSpace(additionalContext)
            ? string.Empty
            : $"""

            ## Additional Context
            {additionalContext}
            """;

        var projectBriefSection = string.IsNullOrWhiteSpace(project.ProjectBrief)
            ? string.Empty
            : $"""

            ## Project Brief
            {project.ProjectBrief}
            """;

        return $"""
            # {Role} Agent - APMAS Project

            You are the **{Role}** for the "{project.Name}" project.

            ## Your Role
            {GetRoleDescription()}

            ## Working Directory
            {project.WorkingDirectory}
            {projectBriefSection}

            ## APMAS Communication

            You have access to APMAS MCP tools for coordination. **USE THEM.**

            ### Required Tool Usage:

            1. **Heartbeat (every 5 minutes)**
               Call `apmas_heartbeat` while working to signal you're alive.
               ```
               apmas_heartbeat(status: "working", progress: "building index.html")
               ```

            2. **Checkpoint (after each subtask)**
               Call `apmas_checkpoint` to save progress for recovery.
               ```
               apmas_checkpoint(
                 summary: "Completed homepage layout",
                 completedItems: ["header", "hero section"],
                 pendingItems: ["footer", "post grid"]
               )
               ```

            3. **Status Updates (CALL FREQUENTLY)**
               Call `apmas_report_status` whenever you:
               - Start a new task phase: `status: "working", message: "Starting: implementing header component"`
               - Complete a piece of work: `status: "working", message: "Completed: header component done"`
               - Hit any delay or blocker: `status: "blocked", message: "Need clarification", blockedReason: "..."`
               - Make progress on long work (every few minutes of active coding)

               Example:
               ```
               apmas_report_status(status: "working", message: "Starting: database schema design")
               // ... do work ...
               apmas_report_status(status: "working", message: "Completed: database schema, moving to API routes")
               // ... do work ...
               apmas_report_status(status: "done", message: "All tasks complete", artifacts: ["docs/architecture.md"])
               ```

            4. **Completion**
               Call `apmas_complete` when ALL work is done.
               ```
               apmas_complete(summary: "Built all pages", artifacts: ["src/index.html", "src/post.html"])
               ```

            ### Context Management

            If you feel your responses getting shorter or you're losing context:
            1. Immediately call `apmas_checkpoint` with your current progress
            2. Call `apmas_report_status(status: "context_limit", message: "Approaching context limits")`
            3. Stop work - the Supervisor will respawn you with your checkpoint

            ### Getting Help

            If blocked, use `apmas_request_help`:
            ```
            apmas_request_help(helpType: "clarification", issue: "Design spec unclear on button colors")
            ```

            ## Your Task
            {GetTaskDescription()}

            ## Deliverables
            {GetDeliverables()}

            ## Dependencies
            {GetDependencies()}
            {additionalSection}

            ---

            **BEGIN:** Start your work now. Remember to call `apmas_heartbeat` every 5 minutes.
            """;
    }

    /// <summary>
    /// Returns a description of the agent's role and responsibilities.
    /// </summary>
    protected abstract string GetRoleDescription();

    /// <summary>
    /// Returns a description of the task the agent should perform.
    /// </summary>
    protected abstract string GetTaskDescription();

    /// <summary>
    /// Returns a description of the expected deliverables.
    /// </summary>
    protected abstract string GetDeliverables();

    /// <summary>
    /// Returns information about dependencies this agent has on other agents.
    /// </summary>
    protected abstract string GetDependencies();
}
