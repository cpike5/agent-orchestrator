namespace Apmas.Server.Configuration;

/// <summary>
/// Agent roster configuration.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// List of agents in the roster.
    /// </summary>
    public List<AgentDefinition> Roster { get; set; } = new();
}

/// <summary>
/// Definition of an agent in the roster.
/// </summary>
public class AgentDefinition
{
    /// <summary>
    /// Unique role name for the agent (e.g., "architect", "developer").
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The Claude Code subagent type to use (e.g., "systems-architect", "html-prototyper").
    /// </summary>
    public string SubagentType { get; set; } = string.Empty;

    /// <summary>
    /// List of agent roles that must complete before this agent can start.
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Optional description of this agent's responsibilities.
    /// </summary>
    public string? Description { get; set; }
}
