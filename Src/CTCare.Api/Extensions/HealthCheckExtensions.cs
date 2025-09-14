using CTCare.Api.Extensions.Utility;

using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using CTCare.Shared.Settings;

namespace CTCare.Api.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthChecksInfra(this IServiceCollection services, IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var dbRaw = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
        var dbConn = Helper.NormalizePostgresForRender(dbRaw);

        var redisSettings = new RedisSetting();
        cfg.GetSection(nameof(RedisSetting)).Bind(redisSettings);

        if (string.IsNullOrWhiteSpace(redisSettings.ConnectionString))
        {
            throw new InvalidOperationException("RedisSetting:ConnectionString is required for health check.");
        }

        var (endpoint, redisPwd) = Helper.NormalizeRedis(
            env.IsProduction() || IsRunningInContainer()
                ? redisSettings.ConnectionString
                : redisSettings.ConnectionStringLocalDEv,
            redisSettings.Password
        );

        var redisHc = string.IsNullOrEmpty(redisPwd)
            ? $"{endpoint},abortConnect=false"
            : $"{endpoint},password={redisPwd},abortConnect=false";

        services.AddHealthChecks()
            .AddNpgSql(dbConn, name: "postgresql", failureStatus: HealthStatus.Unhealthy)
            .AddRedis(redisHc, name: "redis", failureStatus: HealthStatus.Unhealthy);

        return services;
    }

    private static bool IsRunningInContainer() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true",
            StringComparison.OrdinalIgnoreCase);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }
}
