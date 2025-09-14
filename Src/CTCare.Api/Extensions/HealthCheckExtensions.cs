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
        // Read configuration
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        // --- Postgres connection (handle postgres:// URLs on Render) ---
        var dbRaw = cfg.GetConnectionString("DefaultConnection") ?? string.Empty;
        var dbConn = env.IsProduction() ? NormalizePostgres(dbRaw) : dbRaw;

        // --- Redis options (support both host:port and redis:// formats) ---
        var redisSettings = new RedisSetting();
        cfg.GetSection(nameof(RedisSetting)).Bind(redisSettings);

        if (string.IsNullOrWhiteSpace(redisSettings.ConnectionString))
        {
            throw new InvalidOperationException("RedisSetting:ConnectionString is required for health check.");
        }

        var (endpoint, redisPassword) = RedisExtensions.NormalizeRedis(
            redisSettings.ConnectionString,
            redisSettings.Password
        );

        // health-check expects a string like "host:port[,password=***][,abortConnect=false]..."
        var redisHcString = string.IsNullOrEmpty(redisPassword)
            ? $"{endpoint},abortConnect=false"
            : $"{endpoint},password={redisPassword},abortConnect=false";

        redisHcString = env.IsProduction() || IsRunningInContainer()
            ? redisHcString
            : redisSettings.ConnectionStringLocalDEv;

        services.AddHealthChecks()
            .AddNpgSql(dbConn, name: "postgresql", failureStatus: HealthStatus.Unhealthy)
            .AddRedis(redisHcString, name: "redis", failureStatus: HealthStatus.Unhealthy);

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

    // Convert postgres://user:pass@host:port/db?params -> key=value;key=value...
    private static string NormalizePostgres(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }

        var uri = new Uri(raw);

        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? "");
        var pass = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "");

        var b = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = user,
            Password = pass,
            Database = uri.AbsolutePath.Trim('/'),

            // If you’re hitting a public endpoint that requires TLS:
            // SslMode = SslMode.Require,
            // TrustServerCertificate = true
        };

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        foreach (var kv in query)
        {
            var val = kv.Value.Count > 0 ? kv.Value[0] : null;
            if (!string.IsNullOrWhiteSpace(val))
            {
                b[kv.Key] = val;
            }
        }

        return b.ToString();
    }

    private static bool IsRunningInContainer() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase);
}
