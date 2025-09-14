using CTCare.Api.Extensions.Utility;

using Hangfire;
using Hangfire.PostgreSql;

namespace CTCare.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfirePostgres(this IServiceCollection services, IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var raw = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
        var conn = Helper.NormalizePostgresForRender(raw);

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Hangfire connection string is missing.");
        }

        var retries = 0;
        while (true)
        {
            try
            {
                services.AddHangfire(c =>
                    c.UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(conn, new Hangfire.PostgreSql.PostgreSqlStorageOptions
                        {
                            SchemaName = "hangfire",
                            PrepareSchemaIfNecessary = true,
                            QueuePollInterval = TimeSpan.FromSeconds(5)
                        }));
                services.AddHangfireServer();
                return services;
            }
            catch when (retries++ < 5)
            {
                Console.WriteLine($"[Hangfire] Waiting for Postgres… attempt {retries}/5");
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }
}
