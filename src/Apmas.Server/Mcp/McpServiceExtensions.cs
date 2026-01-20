using Microsoft.Extensions.DependencyInjection;

namespace Apmas.Server.Mcp;

/// <summary>
/// Extension methods for registering MCP services with dependency injection.
/// </summary>
public static class McpServiceExtensions
{
    /// <summary>
    /// Adds MCP server services to the service collection.
    /// </summary>
    public static IServiceCollection AddMcpServer(this IServiceCollection services)
    {
        // Register tool registry as singleton
        services.AddSingleton<McpToolRegistry>();

        // Register MCP server host as a hosted service
        services.AddHostedService<McpServerHost>();

        return services;
    }

    /// <summary>
    /// Registers an MCP tool implementation with the tool registry.
    /// </summary>
    /// <typeparam name="TImplementation">The tool implementation type.</typeparam>
    public static IServiceCollection AddMcpTool<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IMcpTool
    {
        // Register the tool as a singleton
        services.AddSingleton<IMcpTool, TImplementation>();

        return services;
    }
}
