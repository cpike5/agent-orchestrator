using System.Text.Json;
using Apmas.Server.Agents.Definitions;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Enums;
using Apmas.Server.Core.Models;
using Apmas.Server.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Manages agent and project state with caching and persistence.
/// </summary>
public class AgentStateManager : IAgentStateManager
{
    private readonly IStateStore _stateStore;
    private readonly ILogger<AgentStateManager> _logger;
    private readonly IMemoryCache _cache;
    private readonly ApmasOptions _options;
    private readonly AgentRoster _roster;
    private readonly IProjectBriefLoader _briefLoader;

    private const string ProjectStateCacheKey = "project-state";
    private const string AllAgentsCacheKey = "all-agents";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public AgentStateManager(
        IStateStore stateStore,
        ILogger<AgentStateManager> logger,
        IMemoryCache cache,
        IOptions<ApmasOptions> options,
        AgentRoster roster,
        IProjectBriefLoader briefLoader)
    {
        _stateStore = stateStore;
        _logger = logger;
        _cache = cache;
        _options = options.Value;
        _roster = roster;
        _briefLoader = briefLoader;
    }

    public async Task<ProjectState> GetProjectStateAsync()
    {
        if (_cache.TryGetValue(ProjectStateCacheKey, out ProjectState? cached) && cached != null)
        {
            _logger.LogDebug("Retrieved project state from cache");
            return cached;
        }

        var state = await _stateStore.GetProjectStateAsync();
        if (state == null)
        {
            _logger.LogWarning("Project state not found in storage");
            throw new InvalidOperationException("Project has not been initialized");
        }

        _cache.Set(ProjectStateCacheKey, state, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        _logger.LogDebug("Retrieved project state from storage and cached it");
        return state;
    }

    public async Task<AgentState> GetAgentStateAsync(string agentRole)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));

        var cacheKey = GetAgentCacheKey(agentRole);
        if (_cache.TryGetValue(cacheKey, out AgentState? cached) && cached != null)
        {
            _logger.LogDebug("Retrieved agent state for {AgentRole} from cache", agentRole);
            return cached;
        }

        var state = await _stateStore.GetAgentStateAsync(agentRole);
        if (state == null)
        {
            _logger.LogWarning("Agent state not found for role {AgentRole}", agentRole);
            throw new InvalidOperationException($"Agent with role '{agentRole}' not found");
        }

        _cache.Set(cacheKey, state, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        _logger.LogDebug("Retrieved agent state for {AgentRole} from storage and cached it", agentRole);
        return state;
    }

    public async Task UpdateAgentStateAsync(string agentRole, AgentState state)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));
        ArgumentNullException.ThrowIfNull(state);

        if (!string.Equals(state.Role, agentRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Agent role mismatch: expected '{agentRole}', but state has '{state.Role}'", nameof(state));
        }

        var oldState = await _stateStore.GetAgentStateAsync(agentRole);
        var oldStatus = oldState?.Status;

        await _stateStore.SaveAgentStateAsync(state);

        // Invalidate caches
        InvalidateAgentCache(agentRole);

        if (oldStatus != state.Status)
        {
            _logger.LogInformation("Agent {AgentRole} transitioned from {OldStatus} to {NewStatus}",
                agentRole, oldStatus, state.Status);
        }
        else
        {
            _logger.LogDebug("Updated agent state for {AgentRole}", agentRole);
        }
    }

    public async Task UpdateAgentStateAsync(string agentRole, Func<AgentState, AgentState> update)
    {
        if (string.IsNullOrWhiteSpace(agentRole))
            throw new ArgumentException("Agent role cannot be null or empty", nameof(agentRole));
        ArgumentNullException.ThrowIfNull(update);

        var currentState = await GetAgentStateAsync(agentRole);
        var newState = update(currentState);
        await UpdateAgentStateAsync(agentRole, newState);
    }

    public async Task<IReadOnlyList<AgentState>> GetActiveAgentsAsync()
    {
        var allAgents = await GetAllAgentsAsync();
        var activeAgents = allAgents
            .Where(a => a.Status == AgentStatus.Running ||
                       a.Status == AgentStatus.Spawning ||
                       a.Status == AgentStatus.Paused)
            .ToList();

        _logger.LogDebug("Found {Count} active agents", activeAgents.Count);
        return activeAgents;
    }

    public async Task<IReadOnlyList<string>> GetReadyAgentsAsync()
    {
        var allAgents = await GetAllAgentsAsync();

        // Build a map of agent roles to their completion status
        var completionMap = allAgents.ToDictionary(a => a.Role, a => a.Status == AgentStatus.Completed, StringComparer.OrdinalIgnoreCase);

        var readyAgents = allAgents
            .Where(a => a.Status == AgentStatus.Pending || a.Status == AgentStatus.Queued)
            .Where(a => AreDependenciesMet(a, completionMap))
            .Select(a => a.Role)
            .ToList();

        _logger.LogDebug("Found {Count} ready agents", readyAgents.Count);
        return readyAgents;
    }

    public async Task InitializeProjectAsync(string name, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be null or empty", nameof(name));
        if (string.IsNullOrWhiteSpace(workingDirectory))
            throw new ArgumentException("Working directory cannot be null or empty", nameof(workingDirectory));

        var projectState = new ProjectState
        {
            Name = name,
            WorkingDirectory = workingDirectory,
            Phase = ProjectPhase.Initializing,
            StartedAt = DateTime.UtcNow
        };

        await _stateStore.SaveProjectStateAsync(projectState);

        // Invalidate cache
        _cache.Remove(ProjectStateCacheKey);

        _logger.LogInformation("Initialized project {ProjectName} at {WorkingDirectory}",
            name, workingDirectory);
    }

    public async Task<bool> InitializeFromConfigAsync()
    {
        // Check if project already exists
        var existingProject = await _stateStore.GetProjectStateAsync();
        if (existingProject != null)
        {
            _logger.LogInformation("Project {ProjectName} already initialized, skipping initialization",
                existingProject.Name);
            return false;
        }

        _logger.LogInformation("Initializing project {ProjectName} from configuration",
            _options.ProjectName);

        // Create project state
        await InitializeProjectAsync(_options.ProjectName, _options.WorkingDirectory);

        // Load project brief if available
        var brief = await _briefLoader.LoadBriefAsync(_options.WorkingDirectory);
        if (!string.IsNullOrEmpty(brief))
        {
            var state = await _stateStore.GetProjectStateAsync();
            if (state != null)
            {
                state.ProjectBrief = brief;
                await _stateStore.SaveProjectStateAsync(state);
                _logger.LogInformation("Loaded project brief ({Length} chars)", brief.Length);
            }
        }

        // Create agent states from roster
        foreach (var agentDef in _roster.Agents)
        {
            var agentState = new AgentState
            {
                Role = agentDef.Role,
                SubagentType = agentDef.SubagentType,
                Status = AgentStatus.Pending,
                DependenciesJson = JsonSerializer.Serialize(agentDef.Dependencies)
            };

            await _stateStore.SaveAgentStateAsync(agentState);
            _logger.LogDebug("Created agent state for {Role} with dependencies {Dependencies}",
                agentDef.Role, agentDef.Dependencies);
        }

        // Invalidate cache
        _cache.Remove(AllAgentsCacheKey);

        _logger.LogInformation("Initialized {Count} agents from roster", _roster.Agents.Count);
        return true;
    }

    public async Task<IReadOnlyList<AgentState>> GetAllAgentsAsync()
    {
        if (_cache.TryGetValue(AllAgentsCacheKey, out IReadOnlyList<AgentState>? cached) && cached != null)
        {
            _logger.LogDebug("Retrieved all agent states from cache");
            return cached;
        }

        var agents = await _stateStore.GetAllAgentStatesAsync();

        _cache.Set(AllAgentsCacheKey, agents, new MemoryCacheEntryOptions
        {
            SlidingExpiration = CacheDuration
        });

        _logger.LogDebug("Retrieved {Count} agent states from storage and cached them", agents.Count);
        return agents;
    }

    private bool AreDependenciesMet(AgentState agent, Dictionary<string, bool> completionMap)
    {
        if (string.IsNullOrEmpty(agent.DependenciesJson))
        {
            return true;
        }

        try
        {
            var dependencies = JsonSerializer.Deserialize<string[]>(agent.DependenciesJson) ?? Array.Empty<string>();

            foreach (var dependency in dependencies)
            {
                if (!completionMap.TryGetValue(dependency, out var isCompleted) || !isCompleted)
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse dependencies JSON for agent {AgentRole}", agent.Role);
            return false;
        }
    }

    private void InvalidateAgentCache(string agentRole)
    {
        _cache.Remove(GetAgentCacheKey(agentRole));
        _cache.Remove(AllAgentsCacheKey);
    }

    private static string GetAgentCacheKey(string agentRole) => $"agent-{agentRole.ToLowerInvariant()}";
}
