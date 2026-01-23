using Apmas.Server.Agents.Definitions;
using Apmas.Server.Agents.Prompts;
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

    /// <summary>
    /// Adds the agent roster configuration to the service collection.
    /// </summary>
    public static IServiceCollection AddAgentRoster(this IServiceCollection services)
    {
        services.AddOptions<AgentOptions>()
            .BindConfiguration("Apmas:Agents")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<AgentRoster>();

        return services;
    }

    /// <summary>
    /// Adds agent prompt templates to the service collection.
    /// </summary>
    public static IServiceCollection AddAgentPrompts(this IServiceCollection services)
    {
        // Register all prompt classes as BaseAgentPrompt for collection injection
        services.AddSingleton<BaseAgentPrompt, DiscoveryPrompt>();
        services.AddSingleton<BaseAgentPrompt, DesignPrepPrompt>();
        services.AddSingleton<BaseAgentPrompt, ArchitectPrompt>();
        services.AddSingleton<BaseAgentPrompt, DesignerPrompt>();
        services.AddSingleton<BaseAgentPrompt, DeveloperPrompt>();
        services.AddSingleton<BaseAgentPrompt, HtmlPrototyperPrompt>();
        services.AddSingleton<BaseAgentPrompt, ReviewerPrompt>();
        services.AddSingleton<BaseAgentPrompt, TaskDeveloperPrompt>();
        services.AddSingleton<BaseAgentPrompt, TesterPrompt>();
        services.AddSingleton<BaseAgentPrompt, UiCriticPrompt>();

        // Register the factory
        services.AddSingleton<IPromptFactory, PromptFactory>();

        return services;
    }
}
