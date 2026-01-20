namespace Apmas.Server.Mcp;

/// <summary>
/// Represents the result of executing an MCP tool.
/// </summary>
public record ToolResult
{
    /// <summary>
    /// Gets or sets the content returned by the tool.
    /// This can be text, structured data, or other content types.
    /// </summary>
    public required IReadOnlyList<ToolResultContent> Content { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the tool execution resulted in an error.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Creates a successful tool result with text content.
    /// </summary>
    public static ToolResult Success(string text) => new()
    {
        Content = new[] { new ToolResultContent { Type = "text", Text = text } },
        IsError = false
    };

    /// <summary>
    /// Creates an error tool result with text content.
    /// </summary>
    public static ToolResult Error(string message) => new()
    {
        Content = new[] { new ToolResultContent { Type = "text", Text = message } },
        IsError = true
    };
}

/// <summary>
/// Represents a content item in a tool result.
/// </summary>
public record ToolResultContent
{
    /// <summary>
    /// Gets or sets the type of content (e.g., "text", "image", "resource").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the text content (for Type = "text").
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the MIME type (for Type = "image" or other binary content).
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Gets or sets the base64-encoded data (for Type = "image" or other binary content).
    /// </summary>
    public string? Data { get; init; }
}
