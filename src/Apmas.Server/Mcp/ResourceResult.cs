namespace Apmas.Server.Mcp;

/// <summary>
/// Represents the result of reading an MCP resource.
/// </summary>
public record ResourceResult
{
    /// <summary>
    /// Gets or sets the content returned by the resource.
    /// </summary>
    public required IReadOnlyList<ResourceContent> Contents { get; init; }

    /// <summary>
    /// Creates a successful resource result with text content.
    /// </summary>
    public static ResourceResult Text(string uri, string text, string mimeType = "application/json") => new()
    {
        Contents = new[]
        {
            new ResourceContent
            {
                Uri = uri,
                MimeType = mimeType,
                Text = text
            }
        }
    };

    /// <summary>
    /// Creates a successful resource result with binary content.
    /// </summary>
    public static ResourceResult Binary(string uri, byte[] data, string mimeType) => new()
    {
        Contents = new[]
        {
            new ResourceContent
            {
                Uri = uri,
                MimeType = mimeType,
                Blob = Convert.ToBase64String(data)
            }
        }
    };

    /// <summary>
    /// Creates an empty result (no content).
    /// </summary>
    public static ResourceResult Empty() => new()
    {
        Contents = Array.Empty<ResourceContent>()
    };
}

/// <summary>
/// Represents a content item in a resource result.
/// </summary>
public record ResourceContent
{
    /// <summary>
    /// Gets or sets the URI of this content.
    /// </summary>
    public required string Uri { get; init; }

    /// <summary>
    /// Gets or sets the MIME type of the content.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Gets or sets the text content (mutually exclusive with Blob).
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the base64-encoded blob content (mutually exclusive with Text).
    /// </summary>
    public string? Blob { get; init; }
}
