using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp;

/// <summary>
/// Registry for discovering and managing MCP resources.
/// </summary>
public class McpResourceRegistry
{
    private readonly List<IMcpResource> _resources;
    private readonly ILogger<McpResourceRegistry> _logger;

    public McpResourceRegistry(IEnumerable<IMcpResource> resources, ILogger<McpResourceRegistry> logger)
    {
        _logger = logger;
        _resources = new List<IMcpResource>();

        foreach (var resource in resources)
        {
            _resources.Add(resource);
            _logger.LogDebug("Registered resource: {ResourceUri}", resource.UriTemplate);
        }

        _logger.LogInformation("Resource registry initialized with {Count} resources", _resources.Count);
    }

    /// <summary>
    /// Gets all registered resources.
    /// </summary>
    public IReadOnlyCollection<IMcpResource> GetAllResources() => _resources;

    /// <summary>
    /// Finds a resource that matches the given URI.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <returns>The matching resource if found, otherwise null.</returns>
    public IMcpResource? FindResource(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        return _resources.FirstOrDefault(r => r.Matches(uri));
    }

    /// <summary>
    /// Checks if any resource matches the given URI.
    /// </summary>
    public bool HasResource(string uri)
    {
        return FindResource(uri) != null;
    }

    /// <summary>
    /// Lists all available resource instances from all registered resources.
    /// </summary>
    public async Task<IReadOnlyList<ResourceDescriptor>> ListAllResourcesAsync(CancellationToken cancellationToken)
    {
        var allDescriptors = new List<ResourceDescriptor>();

        foreach (var resource in _resources)
        {
            try
            {
                var descriptors = await resource.ListAsync(cancellationToken);
                allDescriptors.AddRange(descriptors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing resources for {ResourceUri}", resource.UriTemplate);
            }
        }

        return allDescriptors;
    }
}
