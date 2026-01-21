using Apmas.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Agents.Definitions;

/// <summary>
/// Provides access to the agent roster with validation and lookup capabilities.
/// </summary>
/// <remarks>
/// This class is thread-safe after construction. All collections are immutable after the constructor completes.
/// Multiple threads can safely call any public methods concurrently.
/// </remarks>
public sealed class AgentRoster
{
    private readonly Dictionary<string, AgentDefinition> _agentsByRole;
    private readonly List<AgentDefinition> _agents;

    /// <summary>
    /// Known valid Claude Code subagent types.
    /// </summary>
    public static readonly HashSet<string> ValidSubagentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "systems-architect",
        "design-specialist",
        "html-prototyper",
        "dotnet-specialist",
        "dotnet-fixer",
        "test-writer",
        "code-reviewer",
        "docs-writer",
        "database-specialist",
        "devops-specialist",
        "security-auditor",
        "observability-expert",
        "debug-specialist",
        "ui-critic",
        "content-generator",
        "git-project-manager",
        "general-purpose",
        "Explore",
        "Plan",
        "Bash"
    };

    /// <summary>
    /// Initializes a new AgentRoster from configuration.
    /// </summary>
    /// <param name="options">The agent options containing the roster configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when roster validation fails.</exception>
    public AgentRoster(IOptions<AgentOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _agents = options.Value.Roster;
        _agentsByRole = new Dictionary<string, AgentDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var agent in _agents)
        {
            if (!_agentsByRole.TryAdd(agent.Role, agent))
            {
                throw new InvalidOperationException($"Duplicate agent role: '{agent.Role}'");
            }
        }

        Validate();
    }

    /// <summary>
    /// Gets all agents in the roster.
    /// </summary>
    public IReadOnlyList<AgentDefinition> Agents => _agents.AsReadOnly();

    /// <summary>
    /// Gets an agent by its role name.
    /// </summary>
    /// <param name="role">The role name to look up.</param>
    /// <returns>The agent definition, or null if not found.</returns>
    public AgentDefinition? GetByRole(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return _agentsByRole.GetValueOrDefault(role);
    }

    /// <summary>
    /// Gets an agent by its role name, throwing if not found.
    /// </summary>
    /// <param name="role">The role name to look up.</param>
    /// <returns>The agent definition.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the role is not found.</exception>
    public AgentDefinition GetRequiredByRole(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return _agentsByRole.TryGetValue(role, out var agent)
            ? agent
            : throw new KeyNotFoundException($"Agent role not found: '{role}'");
    }

    /// <summary>
    /// Gets the direct dependencies for an agent.
    /// </summary>
    /// <param name="role">The role to get dependencies for.</param>
    /// <returns>The list of agent definitions that this agent depends on.</returns>
    public IReadOnlyList<AgentDefinition> GetDependencies(string role)
    {
        var agent = GetRequiredByRole(role);
        return agent.Dependencies
            .Select(dep => GetRequiredByRole(dep))
            .ToList();
    }

    /// <summary>
    /// Gets all transitive dependencies for an agent (all agents that must complete first).
    /// </summary>
    /// <param name="role">The role to get all dependencies for.</param>
    /// <returns>The list of all agent definitions in dependency order (dependencies first), excluding the role itself.</returns>
    public IReadOnlyList<AgentDefinition> GetAllDependencies(string role)
    {
        var result = new List<AgentDefinition>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var agent = GetRequiredByRole(role);

        // Collect dependencies of direct dependencies (exclude the root node)
        foreach (var dep in agent.Dependencies)
        {
            CollectDependencies(dep, result, visited);
        }

        return result;
    }

    /// <summary>
    /// Gets all agents that have no dependencies (can start immediately).
    /// </summary>
    public IReadOnlyList<AgentDefinition> GetRootAgents()
    {
        return _agents
            .Where(a => a.Dependencies.Count == 0)
            .ToList();
    }

    /// <summary>
    /// Gets all agents that depend on the specified role.
    /// </summary>
    /// <param name="role">The role to find dependents for.</param>
    /// <returns>The list of agents that directly depend on this role.</returns>
    public IReadOnlyList<AgentDefinition> GetDependents(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return _agents
            .Where(a => a.Dependencies.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Checks if a role exists in the roster.
    /// </summary>
    public bool HasRole(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        return _agentsByRole.ContainsKey(role);
    }

    private void CollectDependencies(string role, List<AgentDefinition> result, HashSet<string> visited)
    {
        if (!visited.Add(role))
        {
            return;
        }

        var agent = GetRequiredByRole(role);

        foreach (var dep in agent.Dependencies)
        {
            CollectDependencies(dep, result, visited);
        }

        result.Add(agent);
    }

    private void Validate()
    {
        var errors = new List<string>();

        // Validate each agent
        foreach (var agent in _agents)
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(agent.Role))
            {
                errors.Add("Agent role cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(agent.SubagentType))
            {
                errors.Add($"Agent '{agent.Role}' has no SubagentType specified");
            }
            else if (!ValidSubagentTypes.Contains(agent.SubagentType))
            {
                errors.Add($"Agent '{agent.Role}' has invalid SubagentType '{agent.SubagentType}'. " +
                    $"Valid types: {string.Join(", ", ValidSubagentTypes.Order())}");
            }

            // Check dependencies exist
            foreach (var dep in agent.Dependencies)
            {
                if (!_agentsByRole.ContainsKey(dep))
                {
                    errors.Add($"Agent '{agent.Role}' depends on unknown role '{dep}'");
                }
            }

            // Check for self-dependency
            if (agent.Dependencies.Contains(agent.Role, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"Agent '{agent.Role}' cannot depend on itself");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Agent roster validation failed:\n- {string.Join("\n- ", errors)}");
        }
    }
}
