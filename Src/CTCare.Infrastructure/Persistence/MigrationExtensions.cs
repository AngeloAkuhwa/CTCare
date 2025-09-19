using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;


namespace CTCare.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations and (optionally) executes a seeder.
    /// Safe to call on every startup
    /// </summary>
    public static async Task MigrateAsync(
        this IServiceProvider services,
        IHostEnvironment env,
        Func<CtCareDbContext, IServiceProvider, Task>? seed = null,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrator");
        var db = sp.GetRequiredService<CtCareDbContext>();

        //resilient retry: wait while Postgres comes up / network jitters
        var retry = Policy
            .Handle<DbException>()
            .Or<NpgsqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 6,
                sleepDurationProvider: i => TimeSpan.FromSeconds(Math.Min(30, 2 * i)),
                onRetry: (ex, delay, attempt, _) =>
                    logger.LogWarning(ex, "DB migrate retry {Attempt}/6, waiting {Delay}s...", attempt, delay.TotalSeconds));

        await retry.ExecuteAsync(async () =>
        {
            var pending = await db.Database.GetPendingMigrationsAsync(ct);
            if (pending.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations...", pending.Count());
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("No pending migrations.");
            }

            if (seed is not null)
            {
                await seed(db, sp);
            }
        });
    }
}
