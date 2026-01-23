using Apmas.Server.Agents;
using Apmas.Server.Configuration;
using Apmas.Server.Core.Services;
using Apmas.Server.Mcp;
using Apmas.Server.Mcp.Resources;
using Apmas.Server.Mcp.Tools;
using Apmas.Server.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "APMAS")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .WriteTo.File(
        path: ".apmas/logs/apmas-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting APMAS server");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, config) => config
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "APMAS")
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Seq("http://localhost:5341")
        .WriteTo.File(
            path: ".apmas/logs/apmas-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7));

    // Configure APMAS options
    builder.Services.Configure<ApmasOptions>(builder.Configuration.GetSection(ApmasOptions.SectionName));

    // Storage
    builder.Services.AddSqliteStateStore();

    // Core services
    builder.Services.AddCoreServices();

    // Configure OpenTelemetry metrics export if enabled
    var metricsOptions = builder.Configuration
        .GetSection(ApmasOptions.SectionName)
        .GetSection("Metrics")
        .Get<MetricsOptions>() ?? new MetricsOptions();
    builder.Services.AddApmasMetricsExport(metricsOptions);

    // Agent roster and spawner
    builder.Services.AddAgentRoster();
    builder.Services.AddAgentPrompts();
    builder.Services.AddAgentSpawner();

    // MCP server
    builder.Services.AddMcpServer();

    // MCP tools
    builder.Services.AddMcpTool<CheckpointTool>();
    builder.Services.AddMcpTool<CompleteTool>();
    builder.Services.AddMcpTool<GetContextTool>();
    builder.Services.AddMcpTool<HeartbeatTool>();
    builder.Services.AddMcpTool<ReportStatusTool>();
    builder.Services.AddMcpTool<RequestHelpTool>();
    builder.Services.AddMcpTool<SendMessageTool>();
    builder.Services.AddMcpTool<SubmitTasksTool>();

    // MCP resources
    builder.Services.AddMcpResource<ProjectStateResource>();
    builder.Services.AddMcpResource<AgentMessagesResource>();
    builder.Services.AddMcpResource<CheckpointResource>();

    // Supervisor service for agent lifecycle management
    builder.Services.AddHostedService<SupervisorService>();

    var host = builder.Build();

    // Ensure database is created
    await host.Services.EnsureStorageCreatedAsync();

    // Initialize project and agents from configuration if not already done
    var stateManager = host.Services.GetRequiredService<IAgentStateManager>();
    var initialized = await stateManager.InitializeFromConfigAsync();
    if (initialized)
    {
        Log.Information("Project and agents initialized from configuration");
    }

    // Validate dependency graph at startup
    var dependencyResolver = host.Services.GetRequiredService<IDependencyResolver>();
    var validationResult = dependencyResolver.ValidateDependencyGraph();

    if (!validationResult.IsValid)
    {
        Log.Error("Dependency graph validation failed:");
        foreach (var error in validationResult.Errors)
        {
            Log.Error("  - {Error}", error);
        }
        Log.Fatal("APMAS cannot start due to invalid dependency configuration");
        return 1;
    }

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "APMAS server terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
