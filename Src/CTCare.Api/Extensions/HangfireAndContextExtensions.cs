using CTCare.Api.Extensions.Utility;
using CTCare.Infrastructure.Persistence;

using Hangfire;
using Hangfire.PostgreSql;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Api.Extensions;

public static class HangfireAndContextExtensions
{
    public static IServiceCollection AddDbContextAndHangfirePostgres(this IServiceCollection services, IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var raw = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
        var conn = Helper.NormalizePostgresForRender(raw);


        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Hangfire connection string is missing.");
        }

        services.AddHttpContextAccessor();

        //Database Context registration
        services.AddDbContext<CtCareDbContext>((sp, opts) =>
        {
            opts.UseNpgsql(conn, npg => npg.MigrationsAssembly(typeof(CtCareDbContext).Assembly.FullName));

            if (env.IsDevelopment())
            {
                opts.EnableDetailedErrors();
                opts.EnableSensitiveDataLogging();
            }
        });

        var retries = 0;

        while (true)
        {
            try
            {
                services.AddHangfire(c =>
                    c.UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(conn, new PostgreSqlStorageOptions
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
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }
    }
}
