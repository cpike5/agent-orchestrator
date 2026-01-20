using Apmas.Server.Configuration;
using Apmas.Server.Core.Services;
using Apmas.Server.Mcp;
using Apmas.Server.Mcp.Tools;
using Apmas.Server.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
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
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
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

    // TODO: Add hosted services (future issues)
    // builder.Services.AddHostedService<SupervisorService>();

    var host = builder.Build();

    // Ensure database is created
    await host.Services.EnsureStorageCreatedAsync();

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
