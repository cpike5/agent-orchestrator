using System.Text.Json;
using System.Text.Json.Nodes;

namespace Apmas.Server.Mcp;

/// <summary>
/// Interface for MCP tool implementations.
/// Tools represent capabilities that agents can invoke via the MCP protocol.
/// </summary>
public interface IMcpTool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON Schema that describes the tool's input parameters.
    /// </summary>
    JsonObject InputSchema { get; }

    /// <summary>
    /// Executes the tool with the provided input.
    /// </summary>
    /// <param name="input">The input arguments as a JSON element.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tool execution result.</returns>
    /// <remarks>
    /// Implementations should return ToolResult with IsError=true for expected errors (e.g., validation failures).
    /// Unexpected exceptions should be thrown and will be caught by the MCP server host,
    /// which will return an appropriate error response to the client.
    /// </remarks>
    Task<ToolResult> ExecuteAsync(JsonElement input, CancellationToken cancellationToken);
}
