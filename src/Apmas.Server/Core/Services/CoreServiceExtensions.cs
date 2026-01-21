using Apmas.Server.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

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
        services.AddSingleton<IHeartbeatMonitor, HeartbeatMonitor>();
        services.AddSingleton<ITimeoutHandler, TimeoutHandler>();
        services.AddSingleton<IContextCheckpointService, ContextCheckpointService>();
        services.AddSingleton<ITaskDecomposerService, TaskDecomposerService>();
        services.AddSingleton<IApmasMetrics, ApmasMetrics>();
        services.AddNotificationServices();
        services.AddHostedService<SupervisorService>();
        return services;
    }

    /// <summary>
    /// Adds notification services based on configuration.
    /// </summary>
    public static IServiceCollection AddNotificationServices(this IServiceCollection services)
    {
        // Register HttpClient for SlackNotificationService
        services.AddHttpClient();

        // Register all implementations as transient so they can be resolved individually
        services.AddTransient<ConsoleNotificationService>();
        services.AddTransient<EmailNotificationService>();
        services.AddTransient<SlackNotificationService>();

        // Register INotificationService using a factory that selects based on config
        services.AddSingleton<INotificationService>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ApmasOptions>>().Value;
            return options.Notifications.Provider switch
            {
                NotificationProvider.Email => sp.GetRequiredService<EmailNotificationService>(),
                NotificationProvider.Slack => sp.GetRequiredService<SlackNotificationService>(),
                _ => sp.GetRequiredService<ConsoleNotificationService>()
            };
        });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry metrics export based on configuration.
    /// Call this method when OpenTelemetry export is enabled in configuration.
    /// </summary>
    public static IServiceCollection AddApmasMetricsExport(this IServiceCollection services, MetricsOptions metricsOptions)
    {
        if (!metricsOptions.OpenTelemetry.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("Apmas.Server"))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("Apmas.Server");

                if (!string.IsNullOrEmpty(metricsOptions.OpenTelemetry.Endpoint))
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(metricsOptions.OpenTelemetry.Endpoint);
                    });
                }
                else
                {
                    // Use default OTLP endpoint (localhost:4317) or OTEL environment variables
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
