using Microsoft.Extensions.DependencyInjection;

namespace Apmas.Server.Core.Services;

/// <summary>
/// Extension methods for registering core services with dependency injection.
/// </summary>
public static class CoreServiceExtensions
{
    /// <summary>
    /// Adds core APMAS services to the service collection.
    /// Note: IAgentSpawner must be registered separately (implementation not in Core layer).
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAgentStateManager, AgentStateManager>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddHostedService<SupervisorService>();
        return services;
    }
}
