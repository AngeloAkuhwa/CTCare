using Hangfire;
using Hangfire.PostgreSql;

namespace CTCare.Api.Extensions;

public static class HangfireExtensions
{
    public static IServiceCollection AddHangfirePostgres(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var config = sp.GetRequiredService<IConfiguration>();
        var conn = config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("Hangfire connection string is missing.");
        }

        var retries = 0;

        while (true)
        {
            try
            {
                services.AddHangfire(cfg =>
                    cfg.UseSimpleAssemblyNameTypeSerializer()
                        .UseRecommendedSerializerSettings()
                        .UsePostgreSqlStorage(conn));
                break;
            }
            catch when (retries++ < 5)
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }


        services.AddHangfireServer();
        return services;
    }
}
