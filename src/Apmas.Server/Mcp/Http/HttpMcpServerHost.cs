using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace Apmas.Server.Mcp.Http;

/// <summary>
/// HTTP/SSE MCP server host that allows spawned Claude agents to connect via HTTP.
/// Implements the MCP protocol over HTTP with Server-Sent Events for server-to-client streaming.
/// </summary>
public class HttpMcpServerHost : BackgroundService
{
    private readonly IOptions<ApmasOptions> _options;
    private readonly McpToolRegistry _toolRegistry;
    private readonly McpResourceRegistry _resourceRegistry;
    private readonly IAgentStateManager _stateManager;
    private readonly IHttpServerReadySignal _readySignal;
    private readonly ILogger<HttpMcpServerHost> _logger;
    private readonly ConcurrentDictionary<string, SseConnection> _connections = new();
    private readonly ConcurrentDictionary<string, ConnectionState> _connectionStates = new();
    private WebApplication? _app;

    private const string ProtocolVersion = "2025-11-25";
    private const string ServerName = "APMAS";
    private const string ServerVersion = "1.0.0";

    public HttpMcpServerHost(
        IOptions<ApmasOptions> options,
        McpToolRegistry toolRegistry,
        McpResourceRegistry resourceRegistry,
        IAgentStateManager stateManager,
        IHttpServerReadySignal readySignal,
        ILogger<HttpMcpServerHost> logger)
    {
        _options = options;
        _toolRegistry = toolRegistry;
        _resourceRegistry = resourceRegistry;
        _stateManager = stateManager;
        _readySignal = readySignal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var httpOptions = _options.Value.HttpTransport;

        if (!httpOptions.Enabled)
        {
            _logger.LogInformation("HTTP MCP transport is disabled");
            _readySignal.SignalReady(); // Signal ready even if disabled so callers don't wait
            return;
        }

        var url = $"http://{httpOptions.Host}:{httpOptions.Port}";
        _logger.LogInformation("Starting HTTP MCP server on {Url}", url);

        try
        {
            var builder = WebApplication.CreateSlimBuilder();

            // Use Serilog for logging instead of default console logger
            builder.Logging.ClearProviders();
            builder.Host.UseSerilog(Log.Logger, dispose: false);

            // Configure Kestrel with request size limits (Issue #2)
            builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = httpOptions.MaxRequestBodySize;
            });

            _app = builder.Build();

            // Configure to listen on the specified URL
            _app.Urls.Clear();
            _app.Urls.Add(url);

            // SSE endpoint for server-to-client streaming
            _app.MapGet("/mcp/sse", HandleSseConnection);

            // POST endpoint for client-to-server messages
            _app.MapPost("/mcp/message", HandleMessageAsync);

            // Health check endpoint
            _app.MapGet("/health", () => Results.Ok(new { status = "healthy", server = ServerName }));

            // Status endpoint for quick visibility into agent states
            _app.MapGet("/status", HandleStatusAsync);

            // Start the server (this returns once it's actually listening)
            await _app.StartAsync(stoppingToken);

            _logger.LogInformation("HTTP MCP server started, verifying it's accepting connections...");

            // Verify the server is actually accepting connections before signaling ready
            var healthUrl = $"{url}/health";
            var verified = await VerifyServerAcceptingConnectionsAsync(healthUrl, stoppingToken);

            if (verified)
            {
                _logger.LogInformation("HTTP MCP server verified and ready on {Url}", url);
                _readySignal.SignalReady();
            }
            else
            {
                _logger.LogError("HTTP MCP server failed verification - agents may not be able to connect");
                _readySignal.SignalReady(); // Signal anyway to avoid blocking forever
            }

            // Wait until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("HTTP MCP server shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP MCP server error");
            throw;
        }
    }

    /// <summary>
    /// Verifies the server is actually accepting connections by hitting the health endpoint.
    /// Retries with exponential backoff up to 5 times.
    /// </summary>
    private async Task<bool> VerifyServerAcceptingConnectionsAsync(string healthUrl, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var maxRetries = 5;
        var baseDelayMs = 100;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Health check passed on attempt {Attempt}", attempt);
                    return true;
                }

                _logger.LogWarning("Health check returned {StatusCode} on attempt {Attempt}",
                    response.StatusCode, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Health check failed on attempt {Attempt}: {Message}", attempt, ex.Message);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Health check timed out on attempt {Attempt}", attempt);
            }

            if (attempt < maxRetries)
            {
                var delayMs = baseDelayMs * (1 << (attempt - 1)); // Exponential backoff: 100, 200, 400, 800ms
                _logger.LogDebug("Retrying health check in {DelayMs}ms...", delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        return false;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping HTTP MCP server, closing {Count} active connections", _connections.Count);

        // Close all active SSE connections
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }
        _connections.Clear();
        _connectionStates.Clear();

        if (_app != null)
        {
            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task HandleSseConnection(HttpContext context)
    {
        var connectionId = Guid.NewGuid().ToString();
        _logger.LogInformation("New SSE connection established: {ConnectionId}", connectionId);

        // Set headers for SSE
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        // Disable response buffering
        var feature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
        feature?.DisableBuffering();

        var connection = new SseConnection(connectionId, context.Response);
        _connections.TryAdd(connectionId, connection);

        // Register connection state (Issue #4)
        var connectionState = new ConnectionState
        {
            ConnectionId = connectionId,
            IsInitialized = false,
            ConnectedAt = DateTime.UtcNow
        };
        _connectionStates.TryAdd(connectionId, connectionState);

        try
        {
            // Send the endpoint event - full URL for the message endpoint
            // Claude Code needs the complete URL to POST messages to
            var httpOptions = _options.Value.HttpTransport;
            var messageUrl = $"http://{httpOptions.Host}:{httpOptions.Port}/mcp/message?connectionId={connectionId}";
            await connection.SendEventAsync("endpoint", messageUrl, context.RequestAborted);

            _logger.LogInformation("Sent endpoint event to connection {ConnectionId}: {Url}", connectionId, messageUrl);

            // Keep connection alive until cancelled
            var keepAliveInterval = TimeSpan.FromSeconds(_options.Value.HttpTransport.SseKeepAliveSeconds);
            while (!context.RequestAborted.IsCancellationRequested)
            {
                // Send periodic keep-alive comments
                await Task.Delay(keepAliveInterval, context.RequestAborted);
                await connection.SendCommentAsync("keep-alive", context.RequestAborted);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSE connection closed: {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE connection {ConnectionId}", connectionId);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            _connectionStates.TryRemove(connectionId, out _);
            connection.Dispose();
        }
    }

    private async Task HandleMessageAsync(HttpContext context)
    {
        try
        {
            // Get connection ID from query parameter (preferred) or header (fallback)
            string? connectionId = context.Request.Query["connectionId"].FirstOrDefault();

            if (string.IsNullOrEmpty(connectionId))
            {
                // Fall back to header for backwards compatibility
                if (context.Request.Headers.TryGetValue("X-Connection-Id", out var connectionIdHeader))
                {
                    connectionId = connectionIdHeader.ToString();
                }
            }

            if (string.IsNullOrEmpty(connectionId))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(
                    CreateErrorResponse(null, -32600, "Invalid Request: missing connectionId query parameter or X-Connection-Id header"),
                    context.RequestAborted);
                return;
            }

            // Verify connection exists and get it
            if (!_connections.TryGetValue(connectionId, out var connection) ||
                !_connectionStates.ContainsKey(connectionId))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(
                    CreateErrorResponse(null, -32600, $"Invalid Request: unknown connection ID {connectionId}"),
                    context.RequestAborted);
                return;
            }

            var message = await JsonSerializer.DeserializeAsync<JsonObject>(
                context.Request.Body,
                cancellationToken: context.RequestAborted);

            if (message == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(
                    CreateErrorResponse(null, -32600, "Invalid Request: empty body"),
                    context.RequestAborted);
                return;
            }

            var response = await ProcessMessageAsync(message, connectionId, context.RequestAborted);

            // MCP SSE Transport: responses go through the SSE stream, not the HTTP response
            // HTTP response should be 202 Accepted (or 204 for notifications)
            if (response != null)
            {
                // Send response through SSE stream as 'message' event
                var responseJson = JsonSerializer.Serialize(response);
                await connection.SendEventAsync("message", responseJson, context.RequestAborted);
                _logger.LogDebug("Sent response via SSE for connection {ConnectionId}", connectionId);

                // Acknowledge receipt with 202 Accepted
                context.Response.StatusCode = 202;
            }
            else
            {
                context.Response.StatusCode = 204; // No Content for notifications
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in request");
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(
                CreateErrorResponse(null, -32700, "Parse error: invalid JSON"),
                context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(
                CreateErrorResponse(null, -32603, $"Internal error: {ex.Message}"),
                context.RequestAborted);
        }
    }

    private async Task<JsonObject?> ProcessMessageAsync(JsonObject message, string connectionId, CancellationToken cancellationToken)
    {
        var id = message["id"];
        var method = message["method"]?.GetValue<string>();

        if (string.IsNullOrEmpty(method))
        {
            return CreateErrorResponse(id, -32600, "Invalid Request: missing method");
        }

        _logger.LogDebug("Processing method: {Method} for connection {ConnectionId}", method, connectionId);

        // Handle initialization notification (Issue #7 - return null for notifications)
        if (method == "notifications/initialized")
        {
            if (_connectionStates.TryGetValue(connectionId, out var state))
            {
                state.IsInitialized = true;
                _logger.LogInformation("Client {ConnectionId} initialization completed", connectionId);
            }
            else
            {
                _logger.LogWarning("Received initialized notification for unknown connection {ConnectionId}", connectionId);
            }
            return null; // No response for notifications
        }

        // Get connection state
        if (!_connectionStates.TryGetValue(connectionId, out var connectionState))
        {
            return CreateErrorResponse(id, -32600, "Invalid connection state");
        }

        // Reject requests if not initialized (except initialize itself)
        if (!connectionState.IsInitialized && method != "initialize")
        {
            return CreateErrorResponse(id, -32002, "Server not initialized. Call initialize first.");
        }

        try
        {
            var response = method switch
            {
                "initialize" => await HandleInitializeAsync(message, connectionId, cancellationToken),
                "tools/list" => await HandleToolsListAsync(message, cancellationToken),
                "tools/call" => await HandleToolsCallAsync(message, cancellationToken),
                "resources/list" => await HandleResourcesListAsync(message, cancellationToken),
                "resources/read" => await HandleResourcesReadAsync(message, cancellationToken),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
            };

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling method {Method}", method);
            return CreateErrorResponse(id, -32603, $"Internal error: {ex.Message}");
        }
    }

    private Task<JsonObject> HandleInitializeAsync(JsonObject request, string connectionId, CancellationToken cancellationToken)
    {
        var id = request["id"];
        var @params = request["params"]?.AsObject();

        var clientVersion = @params?["protocolVersion"]?.GetValue<string>();

        // Validate protocol version
        if (string.IsNullOrEmpty(clientVersion))
        {
            _logger.LogWarning("Client did not provide protocol version");
        }
        else if (!clientVersion.Equals(ProtocolVersion, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Protocol version mismatch. Client: {ClientVersion}, Server: {ServerVersion}",
                clientVersion, ProtocolVersion);

            return Task.FromResult(CreateErrorResponse(id, -32602,
                $"Incompatible protocol version. Server supports {ProtocolVersion}, client requested {clientVersion}"));
        }

        _logger.LogInformation("Client initialized with protocol version {Version}", clientVersion);

        // Mark connection as initialized immediately after successful initialize request
        // Note: We don't wait for notifications/initialized because Claude Code
        // calls tools/list immediately after receiving the initialize response
        if (_connectionStates.TryGetValue(connectionId, out var state))
        {
            state.IsInitialized = true;
            _logger.LogDebug("Connection {ConnectionId} marked as initialized", connectionId);
        }

        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = new JsonObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JsonObject
                {
                    ["tools"] = new JsonObject(),
                    ["resources"] = new JsonObject()
                },
                ["serverInfo"] = new JsonObject
                {
                    ["name"] = ServerName,
                    ["version"] = ServerVersion
                }
            }
        };

        return Task.FromResult(response);
    }

    private Task<JsonObject> HandleToolsListAsync(JsonObject request, CancellationToken cancellationToken)
    {
        var id = request["id"];
        var tools = _toolRegistry.GetAllTools();

        var toolsArray = new JsonArray();
        foreach (var tool in tools)
        {
            var toolObject = new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone()
            };
            toolsArray.Add(toolObject);
        }

        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = new JsonObject
            {
                ["tools"] = toolsArray
            }
        };

        _logger.LogDebug("Returning {Count} tools", tools.Count);
        return Task.FromResult(response);
    }

    private async Task<JsonObject> HandleToolsCallAsync(JsonObject request, CancellationToken cancellationToken)
    {
        var id = request["id"];
        var @params = request["params"]?.AsObject();
        var toolName = @params?["name"]?.GetValue<string>();
        var arguments = @params?["arguments"];

        if (string.IsNullOrEmpty(toolName))
        {
            return CreateErrorResponse(id, -32602, "Invalid params: missing tool name");
        }

        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return CreateErrorResponse(id, -32602, $"Tool not found: {toolName}");
        }

        _logger.LogInformation("Executing tool: {ToolName}", toolName);

        try
        {
            // Convert JsonNode to JsonElement for tool execution
            var argumentsJson = arguments?.ToJsonString() ?? "{}";
            using var doc = JsonDocument.Parse(argumentsJson);
            var result = await tool.ExecuteAsync(doc.RootElement, cancellationToken);

            var contentArray = new JsonArray();
            foreach (var content in result.Content)
            {
                var contentObject = new JsonObject
                {
                    ["type"] = content.Type
                };

                if (content.Text != null)
                {
                    contentObject["text"] = content.Text;
                }
                if (content.MimeType != null)
                {
                    contentObject["mimeType"] = content.MimeType;
                }
                if (content.Data != null)
                {
                    contentObject["data"] = content.Data;
                }

                contentArray.Add(contentObject);
            }

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = new JsonObject
                {
                    ["content"] = contentArray,
                    ["isError"] = result.IsError
                }
            };

            _logger.LogInformation("Tool {ToolName} executed successfully (isError: {IsError})", toolName, result.IsError);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} execution failed", toolName);
            return CreateToolErrorResponse(id, $"Tool execution error: {ex.Message}");
        }
    }

    private async Task<JsonObject> HandleResourcesListAsync(JsonObject request, CancellationToken cancellationToken)
    {
        var id = request["id"];

        var resources = await _resourceRegistry.ListAllResourcesAsync(cancellationToken);

        var resourcesArray = new JsonArray();
        foreach (var resource in resources)
        {
            var resourceObject = new JsonObject
            {
                ["uri"] = resource.Uri,
                ["name"] = resource.Name
            };

            if (resource.Description != null)
            {
                resourceObject["description"] = resource.Description;
            }

            if (resource.MimeType != null)
            {
                resourceObject["mimeType"] = resource.MimeType;
            }

            resourcesArray.Add(resourceObject);
        }

        var response = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = new JsonObject
            {
                ["resources"] = resourcesArray
            }
        };

        _logger.LogDebug("Returning {Count} resources", resources.Count);
        return response;
    }

    private async Task<JsonObject> HandleResourcesReadAsync(JsonObject request, CancellationToken cancellationToken)
    {
        var id = request["id"];
        var @params = request["params"]?.AsObject();
        var uri = @params?["uri"]?.GetValue<string>();

        if (string.IsNullOrEmpty(uri))
        {
            return CreateErrorResponse(id, -32602, "Invalid params: missing resource uri");
        }

        var resource = _resourceRegistry.FindResource(uri);
        if (resource == null)
        {
            return CreateErrorResponse(id, -32602, $"Resource not found: {uri}");
        }

        _logger.LogInformation("Reading resource: {ResourceUri}", uri);

        try
        {
            var result = await resource.ReadAsync(uri, cancellationToken);

            var contentsArray = new JsonArray();
            foreach (var content in result.Contents)
            {
                var contentObject = new JsonObject
                {
                    ["uri"] = content.Uri,
                    ["mimeType"] = content.MimeType
                };

                if (content.Text != null)
                {
                    contentObject["text"] = content.Text;
                }

                if (content.Blob != null)
                {
                    contentObject["blob"] = content.Blob;
                }

                contentsArray.Add(contentObject);
            }

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = new JsonObject
                {
                    ["contents"] = contentsArray
                }
            };

            _logger.LogInformation("Resource {ResourceUri} read successfully", uri);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resource {ResourceUri} read failed", uri);
            return CreateErrorResponse(id, -32603, $"Resource read error: {ex.Message}");
        }
    }

    private JsonObject CreateErrorResponse(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }

    private JsonObject CreateToolErrorResponse(JsonNode? id, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["result"] = new JsonObject
            {
                ["content"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = message
                    }
                },
                ["isError"] = true
            }
        };
    }

    /// <summary>
    /// Represents an active SSE connection.
    /// </summary>
    private class SseConnection : IDisposable
    {
        private readonly string _connectionId;
        private readonly HttpResponse _response;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private volatile bool _isClosed;
        private bool _isDisposed;

        public SseConnection(string connectionId, HttpResponse response)
        {
            _connectionId = connectionId;
            _response = response;
        }

        public async Task SendEventAsync(string eventType, string data, CancellationToken cancellationToken)
        {
            if (_isDisposed || _isClosed) return;

            // Try to wait for the semaphore, but catch ObjectDisposedException (Issue #3)
            try
            {
                await _writeLock.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return; // Semaphore was disposed during wait
            }

            try
            {
                if (_isDisposed || _isClosed) return;

                var message = $"event: {eventType}\ndata: {data}\n\n";
                var bytes = Encoding.UTF8.GetBytes(message);
                await _response.Body.WriteAsync(bytes, cancellationToken);
                await _response.Body.FlushAsync(cancellationToken);
            }
            finally
            {
                if (!_isDisposed)
                {
                    try
                    {
                        _writeLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Semaphore was disposed while we were writing
                    }
                }
            }
        }

        public async Task SendCommentAsync(string comment, CancellationToken cancellationToken)
        {
            if (_isDisposed || _isClosed) return;

            // Try to wait for the semaphore, but catch ObjectDisposedException (Issue #3)
            try
            {
                await _writeLock.WaitAsync(cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return; // Semaphore was disposed during wait
            }

            try
            {
                if (_isDisposed || _isClosed) return;

                var message = $": {comment}\n\n";
                var bytes = Encoding.UTF8.GetBytes(message);
                await _response.Body.WriteAsync(bytes, cancellationToken);
                await _response.Body.FlushAsync(cancellationToken);
            }
            finally
            {
                if (!_isDisposed)
                {
                    try
                    {
                        _writeLock.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Semaphore was disposed while we were writing
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _isClosed = true;
            _writeLock.Dispose();
        }
    }

    /// <summary>
    /// Handles the /status endpoint - provides quick visibility into agent states.
    /// </summary>
    private async Task HandleStatusAsync(HttpContext context)
    {
        try
        {
            var agents = await _stateManager.GetAllAgentsAsync();
            var project = await _stateManager.GetProjectStateAsync();

            var status = new
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                project = project != null ? new
                {
                    name = project.Name,
                    phase = project.Phase.ToString(),
                    workingDirectory = project.WorkingDirectory
                } : null,
                agents = agents.Select(a => new
                {
                    role = a.Role,
                    status = a.Status.ToString(),
                    lastMessage = a.LastMessage,
                    lastHeartbeat = a.LastHeartbeat?.ToString("O"),
                    lastError = a.LastError,
                    taskId = a.TaskId,
                    spawnedAt = a.SpawnedAt?.ToString("O"),
                    retryCount = a.RetryCount
                }).ToList(),
                connections = _connectionStates.Values.Select(c => new
                {
                    id = c.ConnectionId,
                    connectedAt = c.ConnectedAt.ToString("O"),
                    initialized = c.IsInitialized
                }).ToList()
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(status, new JsonSerializerOptions { WriteIndented = true }, context.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling status request");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message }, context.RequestAborted);
        }
    }

    /// <summary>
    /// Represents the state of a client connection.
    /// </summary>
    private class ConnectionState
    {
        public required string ConnectionId { get; init; }
        public bool IsInitialized { get; set; }
        public DateTime ConnectedAt { get; init; }
    }
}
