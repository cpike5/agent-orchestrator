using Apmas.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apmas.Server.Storage;

/// <summary>
/// Extension methods for registering storage services.
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>
    /// Adds SQLite state storage services to the service collection.
    /// </summary>
    public static IServiceCollection AddSqliteStateStore(this IServiceCollection services)
    {
        services.AddDbContextFactory<ApmasDbContext>((serviceProvider, options) =>
        {
            var apmasOptions = serviceProvider.GetRequiredService<IOptions<ApmasOptions>>().Value;
            var dataDir = apmasOptions.GetDataDirectoryPath();
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, "state.db");
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<IStateStore, SqliteStateStore>();

        return services;
    }

    /// <summary>
    /// Ensures the database is created and migrated.
    /// </summary>
    public static async Task EnsureStorageCreatedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApmasDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
