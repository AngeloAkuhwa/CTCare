using HealthChecks.UI.Client;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CTCare.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthChecksInfra(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var db = cfg.GetConnectionString("DefaultConnection");
        var redis = cfg["RedisSetting:ConnectionString"];

        services.AddHealthChecks()
            .AddNpgSql(db, name: "postgresql")
            .AddRedis(redis, name: "redis");

        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }
}
