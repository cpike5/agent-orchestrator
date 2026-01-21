using System.Text.Json;
using System.Text.RegularExpressions;
using Apmas.Server.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Resources;

/// <summary>
/// MCP resource providing read-only access to agent checkpoints.
/// URI: apmas://checkpoints/{agentRole}
/// </summary>
public partial class CheckpointResource : IMcpResource
{
    private readonly IStateStore _stateStore;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CheckpointResource> _logger;

    private const string BaseUri = "apmas://checkpoints";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [GeneratedRegex(@"^apmas://checkpoints/([a-zA-Z0-9_-]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex UriPattern();

    public CheckpointResource(
        IStateStore stateStore,
        IMemoryCache cache,
        ILogger<CheckpointResource> logger)
    {
        _stateStore = stateStore;
        _cache = cache;
        _logger = logger;
    }

    public string UriTemplate => "apmas://checkpoints/{agentRole}";

    public string Name => "Agent Checkpoints";

    public string Description => "Returns the latest checkpoint for a specific agent. URI format: apmas://checkpoints/{agentRole}";

    public string MimeType => "application/json";

    public bool Matches(string uri)
    {
        return UriPattern().IsMatch(uri);
    }

    public async Task<IReadOnlyList<ResourceDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var descriptors = new List<ResourceDescriptor>();

        // List checkpoint resources for each known agent role
        var agentStates = await _stateStore.GetAllAgentStatesAsync();
        foreach (var agent in agentStates)
        {
            descriptors.Add(new ResourceDescriptor
            {
                Uri = $"{BaseUri}/{agent.Role}",
                Name = $"Checkpoint for {agent.Role}",
                Description = $"Latest checkpoint for the {agent.Role} agent",
                MimeType = MimeType
            });
        }

        return descriptors;
    }

    public async Task<ResourceResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        var match = UriPattern().Match(uri);
        if (!match.Success)
        {
            throw new ArgumentException($"URI does not match this resource: {uri}", nameof(uri));
        }

        var agentRole = match.Groups[1].Value;

        _logger.LogDebug("Reading checkpoint resource for agent: {AgentRole}", agentRole);

        // Create cache key
        var cacheKey = $"CheckpointResource:{agentRole}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedJson) && cachedJson != null)
        {
            _logger.LogDebug("Returning cached checkpoint");
            return ResourceResult.Text(uri, cachedJson);
        }

        // Fetch from store
        var checkpoint = await _stateStore.GetLatestCheckpointAsync(agentRole);

        if (checkpoint == null)
        {
            _logger.LogDebug("No checkpoint found for agent: {AgentRole}", agentRole);
            var notFoundResponse = JsonSerializer.Serialize(new
            {
                agentRole,
                message = $"No checkpoint found for agent '{agentRole}'"
            }, JsonOptions);
            return ResourceResult.Text(uri, notFoundResponse);
        }

        // Transform checkpoint to a clean JSON format
        var json = JsonSerializer.Serialize(new
        {
            checkpoint.AgentRole,
            checkpoint.CreatedAt,
            checkpoint.Summary,
            Progress = new
            {
                checkpoint.CompletedTaskCount,
                checkpoint.TotalTaskCount,
                checkpoint.PercentComplete
            },
            CompletedItems = DeserializeJson<List<string>>(checkpoint.CompletedItemsJson) ?? new List<string>(),
            PendingItems = DeserializeJson<List<string>>(checkpoint.PendingItemsJson) ?? new List<string>(),
            ActiveFiles = DeserializeJson<List<string>>(checkpoint.ActiveFilesJson) ?? new List<string>(),
            checkpoint.Notes,
            checkpoint.EstimatedContextUsage
        }, JsonOptions);

        // Cache the result
        _cache.Set(cacheKey, json, CacheDuration);

        _logger.LogDebug("Returning checkpoint for {AgentRole}: {PercentComplete}% complete",
            agentRole, checkpoint.PercentComplete);

        return ResourceResult.Text(uri, json);
    }

    private static T? DeserializeJson<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }
}
