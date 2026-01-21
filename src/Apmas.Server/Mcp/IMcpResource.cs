namespace Apmas.Server.Mcp;

/// <summary>
/// Interface for MCP resource implementations.
/// Resources provide read-only access to data via URI patterns.
/// </summary>
public interface IMcpResource
{
    /// <summary>
    /// Gets the URI template for this resource.
    /// Supports placeholders like {agentRole} for parameterized URIs.
    /// </summary>
    /// <example>
    /// apmas://project/state
    /// apmas://messages/{agentRole}
    /// apmas://checkpoints/{agentRole}
    /// </example>
    string UriTemplate { get; }

    /// <summary>
    /// Gets the name of the resource for display purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what the resource provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the MIME type of the resource content.
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Determines if this resource matches the given URI.
    /// </summary>
    /// <param name="uri">The URI to match.</param>
    /// <returns>True if the resource can handle this URI.</returns>
    bool Matches(string uri);

    /// <summary>
    /// Lists available resource URIs for this resource type.
    /// For static resources, returns the single URI.
    /// For parameterized resources, returns available instances.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available resource descriptors.</returns>
    Task<IReadOnlyList<ResourceDescriptor>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the resource content for the given URI.
    /// </summary>
    /// <param name="uri">The resource URI.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource result containing the content.</returns>
    Task<ResourceResult> ReadAsync(string uri, CancellationToken cancellationToken);
}

/// <summary>
/// Describes an available resource instance.
/// </summary>
public record ResourceDescriptor
{
    /// <summary>
    /// Gets or sets the URI of the resource.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets or sets the display name of the resource.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the description of the resource.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the MIME type of the resource content.
    /// </summary>
    public string? MimeType { get; init; }
}
