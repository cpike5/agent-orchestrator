using System.Text.Json;
using Apmas.Server.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp.Resources;

/// <summary>
/// MCP resource providing read-only access to current project state.
/// URI: apmas://project/state
/// </summary>
public class ProjectStateResource : IMcpResource
{
    private readonly IStateStore _stateStore;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProjectStateResource> _logger;

    private const string ResourceUri = "apmas://project/state";
    private const string CacheKey = "ProjectStateResource";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProjectStateResource(
        IStateStore stateStore,
        IMemoryCache cache,
        ILogger<ProjectStateResource> logger)
    {
        _stateStore = stateStore;
        _cache = cache;
        _logger = logger;
    }

    public string UriTemplate => ResourceUri;

    public string Name => "Project State";

    public string Description => "Returns current project state as JSON including phase, timing, and basic info.";

    public string MimeType => "application/json";

    public bool Matches(string uri)
    {
        return string.Equals(uri, ResourceUri, StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<ResourceDescriptor>> ListAsync(CancellationToken cancellationToken)
    {
        var descriptors = new List<ResourceDescriptor>
        {
            new()
            {
                Uri = ResourceUri,
                Name = Name,
                Description = Description,
                MimeType = MimeType
            }
        };

        return Task.FromResult<IReadOnlyList<ResourceDescriptor>>(descriptors);
    }

    public async Task<ResourceResult> ReadAsync(string uri, CancellationToken cancellationToken)
    {
        if (!Matches(uri))
        {
            throw new ArgumentException($"URI does not match this resource: {uri}", nameof(uri));
        }

        _logger.LogDebug("Reading project state resource");

        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out string? cachedJson) && cachedJson != null)
        {
            _logger.LogDebug("Returning cached project state");
            return ResourceResult.Text(uri, cachedJson);
        }

        // Fetch from store
        var projectState = await _stateStore.GetProjectStateAsync();

        if (projectState == null)
        {
            _logger.LogWarning("No project state found");
            var emptyResponse = JsonSerializer.Serialize(new { message = "No project state found" }, JsonOptions);
            return ResourceResult.Text(uri, emptyResponse);
        }

        // Serialize to JSON
        var json = JsonSerializer.Serialize(new
        {
            projectState.Name,
            projectState.WorkingDirectory,
            Phase = projectState.Phase.ToString(),
            projectState.StartedAt,
            projectState.CompletedAt
        }, JsonOptions);

        // Cache the result
        _cache.Set(CacheKey, json, CacheDuration);

        _logger.LogDebug("Returning project state: {Name}, Phase: {Phase}", projectState.Name, projectState.Phase);

        return ResourceResult.Text(uri, json);
    }
}
