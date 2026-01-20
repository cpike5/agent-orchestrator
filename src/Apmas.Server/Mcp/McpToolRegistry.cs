using Microsoft.Extensions.Logging;

namespace Apmas.Server.Mcp;

/// <summary>
/// Registry for discovering and managing MCP tools.
/// </summary>
public class McpToolRegistry
{
    private readonly Dictionary<string, IMcpTool> _tools;
    private readonly ILogger<McpToolRegistry> _logger;

    public McpToolRegistry(IEnumerable<IMcpTool> tools, ILogger<McpToolRegistry> logger)
    {
        _logger = logger;
        _tools = new Dictionary<string, IMcpTool>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            if (_tools.ContainsKey(tool.Name))
            {
                _logger.LogWarning("Duplicate tool name detected: {ToolName}. Skipping registration.", tool.Name);
                continue;
            }

            _tools[tool.Name] = tool;
            _logger.LogDebug("Registered tool: {ToolName}", tool.Name);
        }

        _logger.LogInformation("Tool registry initialized with {Count} tools", _tools.Count);
    }

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IReadOnlyCollection<IMcpTool> GetAllTools() => _tools.Values;

    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <returns>The tool if found, otherwise null.</returns>
    public IMcpTool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    /// <summary>
    /// Checks if a tool with the given name exists.
    /// </summary>
    public bool HasTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _tools.ContainsKey(name);
    }
}
