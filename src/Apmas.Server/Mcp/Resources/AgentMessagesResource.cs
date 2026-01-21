using System.Text.Json;
using System.Text.RegularExpressions;
using Apmas.Server.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Resources;

/// <summary>
/// MCP resource providing read-only access to agent messages.
/// URI: apmas://messages or apmas://messages/{agentRole}
/// </summary>
public partial class AgentMessagesResource : IMcpResource
{
    private readonly IStateStore _stateStore;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AgentMessagesResource> _logger;

    private const string BaseUri = "apmas://messages";
    private const int DefaultMessageLimit = 100;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [GeneratedRegex(@"^apmas://messages(?:/([a-zA-Z0-9_-]+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex UriPattern();

    public AgentMessagesResource(
        IStateStore stateStore,
        IMemoryCache cache,
        ILogger<AgentMessagesResource> logger)
    {
        _stateStore = stateStore;
        _cache = cache;
        _logger = logger;
    }

    public string UriTemplate => "apmas://messages/{agentRole?}";

    public string Name => "Agent Messages";

    public string Description => "Returns messages for a specific agent or all messages. Use apmas://messages for all, or apmas://messages/{agentRole} for filtered.";

    public string MimeType => "application/json";

    public bool Matches(string uri)
    {
        return UriPattern().IsMatch(uri);
    }

    public async Task<IReadOnlyList<ResourceDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var descriptors = new List<ResourceDescriptor>
        {
            // Always include the "all messages" resource
            new()
            {
                Uri = BaseUri,
                Name = "All Messages",
                Description = "All agent messages",
                MimeType = MimeType
            }
        };

        // List resources for each known agent role
        var agentStates = await _stateStore.GetAllAgentStatesAsync();
        foreach (var agent in agentStates)
        {
            descriptors.Add(new ResourceDescriptor
            {
                Uri = $"{BaseUri}/{agent.Role}",
                Name = $"Messages for {agent.Role}",
                Description = $"Messages to/from the {agent.Role} agent",
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

        // Extract optional agent role from URI
        string? agentRole = match.Groups[1].Success ? match.Groups[1].Value : null;

        _logger.LogDebug("Reading messages resource for agent: {AgentRole}", agentRole ?? "all");

        // Create cache key
        var cacheKey = $"AgentMessagesResource:{agentRole ?? "all"}";

        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out string? cachedJson) && cachedJson != null)
        {
            _logger.LogDebug("Returning cached messages");
            return ResourceResult.Text(uri, cachedJson);
        }

        // Fetch from store
        var messages = await _stateStore.GetMessagesAsync(
            role: agentRole,
            since: null,
            limit: DefaultMessageLimit);

        // Transform messages to a clean JSON format
        var messageData = messages.Select(m => new
        {
            m.Id,
            m.Timestamp,
            m.From,
            m.To,
            Type = m.Type.ToString(),
            m.Content,
            Artifacts = DeserializeJson<List<string>>(m.ArtifactsJson),
            Metadata = DeserializeJson<Dictionary<string, object>>(m.MetadataJson)
        }).ToList();

        var json = JsonSerializer.Serialize(new
        {
            filter = agentRole ?? "all",
            count = messageData.Count,
            limit = DefaultMessageLimit,
            messages = messageData
        }, JsonOptions);

        // Cache the result
        _cache.Set(cacheKey, json, CacheDuration);

        _logger.LogDebug("Returning {Count} messages for {AgentRole}", messageData.Count, agentRole ?? "all");

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
