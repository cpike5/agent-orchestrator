using System.ComponentModel.DataAnnotations;

namespace Apmas.Server.Configuration;

/// <summary>
/// Configuration options for HTTP/SSE MCP transport for spawned agents.
/// </summary>
public class HttpTransportOptions
{
    /// <summary>
    /// Whether to enable HTTP transport for spawned agents.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Port for the HTTP MCP endpoint. Must be between 1 and 65535.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 5050;

    /// <summary>
    /// Host to bind to. Use "localhost" for local-only access.
    /// WARNING: Using "0.0.0.0" or "*" exposes the server to network access without authentication.
    /// Only use non-localhost bindings in trusted network environments.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Interval in seconds for SSE keep-alive comments. Default is 30 seconds.
    /// </summary>
    [Range(5, 300, ErrorMessage = "SseKeepAliveSeconds must be between 5 and 300")]
    public int SseKeepAliveSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum request body size in bytes. Default is 10MB.
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 10 * 1024 * 1024;
}
