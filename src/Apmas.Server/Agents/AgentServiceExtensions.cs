using Apmas.Server.Configuration;
using Apmas.Server.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Apmas.Server.Agents;

/// <summary>
/// Extension methods for registering agent spawner services with dependency injection.
/// </summary>
public static class AgentServiceExtensions
{
    /// <summary>
    /// Adds the Claude Code agent spawner to the service collection.
    /// </summary>
    public static IServiceCollection AddAgentSpawner(this IServiceCollection services)
    {
        services.AddOptions<SpawnerOptions>()
            .BindConfiguration("Apmas:Spawner")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAgentSpawner, ClaudeCodeSpawner>();

        return services;
    }
}
