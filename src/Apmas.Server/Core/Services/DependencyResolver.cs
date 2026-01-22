using System.Text.Json;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Resolves agent dependencies and validates the dependency graph.
/// </summary>
public class DependencyResolver : IDependencyResolver
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<DependencyResolver> _logger;
    private readonly Dictionary<string, List<string>> _dependencyMap;

    public DependencyResolver(
        IStateStore stateStore,
        IOptions<ApmasOptions> options,
        ILogger<DependencyResolver> logger)
    {
        _stateStore = stateStore;
        _logger = logger;

        // Build dependency map from roster at construction time
        var roster = options.Value.Agents.Roster ?? [];
        _dependencyMap = roster.ToDictionary(a => a.Role, a => a.Dependencies, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetReadyAgentsAsync()
    {
        var allAgents = await _stateStore.GetAllAgentStatesAsync();

        // Build completion status map
        var completionMap = allAgents
            .ToDictionary(a => a.Role, a => a.Status == AgentStatus.Completed, StringComparer.OrdinalIgnoreCase);

        var readyAgents = allAgents
            .Where(a => a.Status == AgentStatus.Pending)
            .Where(a => AreDependenciesMet(a.Role, a.DependenciesJson, completionMap))
            .Select(a => a.Role)
            .ToList();

        _logger.LogDebug("Found {Count} agents ready to spawn", readyAgents.Count);
        return readyAgents;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetDependencies(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return Array.Empty<string>();

        return _dependencyMap.TryGetValue(role, out var deps)
            ? deps.AsReadOnly()
            : Array.Empty<string>();
    }

    /// <inheritdoc />
    public DependencyValidationResult ValidateDependencyGraph()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 1. Check for missing agent definitions
        var definedRoles = new HashSet<string>(_dependencyMap.Keys);

        foreach (var (role, dependencies) in _dependencyMap)
        {
            foreach (var dep in dependencies)
            {
                if (!definedRoles.Contains(dep))
                {
                    errors.Add($"Agent '{role}' depends on '{dep}', but '{dep}' is not defined in the roster.");
                }
            }
        }

        // 2. Check for circular dependencies using DFS with 3-color marking
        var circularDeps = DetectCircularDependencies();
        errors.AddRange(circularDeps);

        // 3. Log the dependency graph
        LogDependencyGraph();

        return new DependencyValidationResult
        {
            Errors = errors,
            Warnings = warnings
        };
    }

    private bool AreDependenciesMet(string role, string? dependenciesJson, Dictionary<string, bool> completionMap)
    {
        // First check DependenciesJson (from persisted agent state)
        if (!string.IsNullOrEmpty(dependenciesJson))
        {
            try
            {
                var deps = JsonSerializer.Deserialize<string[]>(dependenciesJson) ?? Array.Empty<string>();
                return deps.All(dep => completionMap.TryGetValue(dep, out var completed) && completed);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse dependencies JSON for agent {Role}", role);
                return false;
            }
        }

        // Fall back to roster definition
        if (_dependencyMap.TryGetValue(role, out var rosterDeps))
        {
            return rosterDeps.All(dep => completionMap.TryGetValue(dep, out var completed) && completed);
        }

        // No dependencies defined = ready
        return true;
    }

    private List<string> DetectCircularDependencies()
    {
        var errors = new List<string>();

        // 0 = white (unvisited), 1 = gray (in progress), 2 = black (done)
        var color = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in _dependencyMap.Keys)
        {
            color[role] = 0;
        }

        foreach (var role in _dependencyMap.Keys)
        {
            if (color[role] == 0)
            {
                var recursionStack = new List<string>();
                if (HasCycle(role, color, recursionStack, out var cycleStart))
                {
                    // Build the cycle path from where the cycle starts
                    var cycleStartIndex = recursionStack.IndexOf(cycleStart);
                    var cyclePath = recursionStack.Skip(cycleStartIndex).ToList();
                    cyclePath.Add(cycleStart); // Complete the cycle
                    errors.Add($"Circular dependency detected: {string.Join(" -> ", cyclePath)}");
                }
            }
        }

        return errors;
    }

    private bool HasCycle(string role, Dictionary<string, int> color, List<string> recursionStack, out string cycleStart)
    {
        cycleStart = string.Empty;
        color[role] = 1; // Mark as in progress (gray)
        recursionStack.Add(role);

        if (_dependencyMap.TryGetValue(role, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                // Skip dependencies not in the roster (handled by missing definition check)
                if (!color.ContainsKey(dep))
                    continue;

                if (color[dep] == 1) // Found a back edge (cycle)
                {
                    cycleStart = dep;
                    return true;
                }

                if (color[dep] == 0 && HasCycle(dep, color, recursionStack, out cycleStart))
                {
                    return true;
                }
            }
        }

        recursionStack.RemoveAt(recursionStack.Count - 1);
        color[role] = 2; // Mark as done (black)
        return false;
    }

    private void LogDependencyGraph()
    {
        _logger.LogInformation("=== Agent Dependency Graph ===");

        foreach (var (role, deps) in _dependencyMap.OrderBy(kv => kv.Key))
        {
            if (deps.Count == 0)
            {
                _logger.LogInformation("  {Role}: (no dependencies)", role);
            }
            else
            {
                _logger.LogInformation("  {Role}: depends on [{Dependencies}]",
                    role, string.Join(", ", deps));
            }
        }

        _logger.LogInformation("==============================");
    }
}
