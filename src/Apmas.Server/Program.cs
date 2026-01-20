using Apmas.Server.Configuration;
using Apmas.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

    // Configure EF Core with SQLite
    builder.Services.AddDbContext<ApmasDbContext>((serviceProvider, options) =>
    {
        var apmasOptions = serviceProvider.GetRequiredService<IOptions<ApmasOptions>>().Value;
        var dataDirectory = apmasOptions.GetDataDirectoryPath();

        // Ensure data directory exists
        Directory.CreateDirectory(dataDirectory);

        var dbPath = Path.Combine(dataDirectory, "state.db");
        options.UseSqlite($"Data Source={dbPath}");
    });

    // TODO: Add core services (future issues)
    // builder.Services.AddSingleton<IAgentStateManager, AgentStateManager>();
    // builder.Services.AddSingleton<IMessageBus, MessageBus>();
    // builder.Services.AddHostedService<SupervisorService>();

    var host = builder.Build();

    // Initialize database
    using (var scope = host.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApmasDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        Log.Information("Database initialized at {DatabasePath}",
            dbContext.Database.GetDbConnection().DataSource);
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
