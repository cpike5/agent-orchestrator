using Microsoft.Extensions.DependencyInjection;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Extension methods for registering core services with dependency injection.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Adds core APMAS services to the service collection.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAgentStateManager, AgentStateManager>();
        return services;
    }
}
