using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp;

/// <summary>
/// MCP server host that handles stdio transport and JSON-RPC message processing.
/// </summary>
public class McpServerHost : BackgroundService
{
    private readonly McpToolRegistry _toolRegistry;
    private readonly McpResourceRegistry _resourceRegistry;
    private readonly ILogger<McpServerHost> _logger;
    private readonly Stream _inputStream;
    private readonly Stream _outputStream;
    private readonly SemaphoreSlim _outputLock = new(1, 1);
    private readonly ConcurrentDictionary<Task, byte> _activeTasks = new();
    private bool _isInitialized = false;

    private const string ProtocolVersion = "2025-11-25";
    private const string ServerName = "APMAS";
    private const string ServerVersion = "1.0.0";

    public McpServerHost(
        McpToolRegistry toolRegistry,
        McpResourceRegistry resourceRegistry,
        ILogger<McpServerHost> logger)
    {
        _toolRegistry = toolRegistry;
        _resourceRegistry = resourceRegistry;
        _logger = logger;
        _inputStream = Console.OpenStandardInput();
        _outputStream = Console.OpenStandardOutput();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MCP server starting on stdio transport");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await ReadMessageAsync(stoppingToken);
                if (message == null)
                {
                    _logger.LogDebug("Received null message, connection closed");
                    break;
                }

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessMessageAsync(message, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                    }
                }, stoppingToken);
                _activeTasks.TryAdd(task, 0);

                // Clean up completed tasks to prevent memory leaks
                _ = task.ContinueWith(t => _activeTasks.TryRemove(t, out _), TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MCP server shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP server error");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for active tasks to complete");
        await Task.WhenAll(_activeTasks.Keys.ToArray());
        await base.StopAsync(cancellationToken);
    }

    private async Task<JsonObject?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Read headers byte-by-byte to avoid StreamReader buffering issues
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var headerBytes = new List<byte>();
            var consecutiveCrLf = 0;

            // Read until we find \r\n\r\n (end of headers)
            while (consecutiveCrLf < 4)
            {
                var singleByteBuffer = new byte[1];
                var bytesRead = await _inputStream.ReadAsync(singleByteBuffer.AsMemory(), cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogDebug("Stream closed while reading headers");
                    return null;
                }

                var b = singleByteBuffer[0];
                headerBytes.Add(b);

                // Track \r\n\r\n sequence
                if ((consecutiveCrLf == 0 || consecutiveCrLf == 2) && b == '\r')
                {
                    consecutiveCrLf++;
                }
                else if ((consecutiveCrLf == 1 || consecutiveCrLf == 3) && b == '\n')
                {
                    consecutiveCrLf++;
                }
                else
                {
                    consecutiveCrLf = 0;
                }
            }

            // Parse headers (remove the final \r\n\r\n)
            var headerText = Encoding.UTF8.GetString(headerBytes.ToArray(), 0, headerBytes.Count - 4);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line[..colonIndex].Trim();
                    var value = line[(colonIndex + 1)..].Trim();
                    headers[key] = value;
                }
            }

            if (!headers.TryGetValue("Content-Length", out var contentLengthStr) ||
                !int.TryParse(contentLengthStr, out var contentLength))
            {
                _logger.LogWarning("Missing or invalid Content-Length header");
                return null;
            }

            // Validate Content-Type header (fail if invalid)
            if (!headers.TryGetValue("Content-Type", out var contentType) ||
                !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid or missing Content-Type header. Expected application/json, got: {ContentType}", contentType);
                return null;
            }

            // Read the JSON payload
            var buffer = new byte[contentLength];
            var totalRead = 0;
            while (totalRead < contentLength)
            {
                var bytesRead = await _inputStream.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), cancellationToken);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("Unexpected end of stream while reading message body");
                    return null;
                }
                totalRead += bytesRead;
            }

            var json = Encoding.UTF8.GetString(buffer);
            _logger.LogDebug("Received message: {Json}", json);

            return JsonNode.Parse(json)?.AsObject();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading message");
            return null;
        }
    }

    private async Task ProcessMessageAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var id = message["id"];
        var method = message["method"]?.GetValue<string>();

        if (string.IsNullOrEmpty(method))
        {
            await SendErrorResponseAsync(id, -32600, "Invalid Request: missing method", cancellationToken);
            return;
        }

        _logger.LogDebug("Processing method: {Method}", method);

        // Handle initialization notification (no response required)
        if (method == "notifications/initialized")
        {
            _isInitialized = true;
            _logger.LogInformation("Client initialization completed");
            return;
        }

        // Reject requests if not initialized (except initialize itself)
        if (!_isInitialized && method != "initialize")
        {
            await SendErrorResponseAsync(id, -32002, "Server not initialized. Call initialize first.", cancellationToken);
            return;
        }

        try
        {
            var response = method switch
            {
                "initialize" => await HandleInitializeAsync(message, cancellationToken),
                "tools/list" => await HandleToolsListAsync(message, cancellationToken),
                "tools/call" => await HandleToolsCallAsync(message, cancellationToken),
                "resources/list" => await HandleResourcesListAsync(message, cancellationToken),
                "resources/read" => await HandleResourcesReadAsync(message, cancellationToken),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
            };

            await SendResponseAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling method {Method}", method);
            await SendErrorResponseAsync(id, -32603, $"Internal error: {ex.Message}", cancellationToken);
        }
    }

    private Task<JsonObject> HandleInitializeAsync(JsonObject request, CancellationToken cancellationToken)
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

    private async Task SendErrorResponseAsync(JsonNode? id, int code, string message, CancellationToken cancellationToken)
    {
        var response = CreateErrorResponse(id, code, message);
        await SendResponseAsync(response, cancellationToken);
    }

    private async Task SendResponseAsync(JsonObject response, CancellationToken cancellationToken)
    {
        await _outputLock.WaitAsync(cancellationToken);
        try
        {
            var json = response.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            var jsonBytes = Encoding.UTF8.GetBytes(json);

            var headers = $"Content-Length: {jsonBytes.Length}\r\nContent-Type: application/json\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(headers);

            await _outputStream.WriteAsync(headerBytes, cancellationToken);
            await _outputStream.WriteAsync(jsonBytes, cancellationToken);
            await _outputStream.FlushAsync(cancellationToken);

            _logger.LogDebug("Sent response: {Json}", json);
        }
        finally
        {
            _outputLock.Release();
        }
    }


    public override void Dispose()
    {
        _outputLock.Dispose();
        base.Dispose();
    }
}
