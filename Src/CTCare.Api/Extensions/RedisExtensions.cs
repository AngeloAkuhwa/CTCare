using System.Net;

using CTCare.Shared.Settings;

using StackExchange.Redis;

namespace CTCare.Api.Extensions;

public static class RedisExtensions
{
    public static IServiceCollection AddRedisCaching(this IServiceCollection services, IHostEnvironment env)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var settings = new RedisSetting();
        cfg.GetSection(nameof(RedisSetting)).Bind(settings);

        services.AddSingleton(settings);

        // Normalize support for "redis://user:pass@host:port"
        var (endpoint, password) =
            NormalizeRedis(
                env.IsProduction() || IsRunningInContainer()
                    ? settings.ConnectionString
                    : settings.ConnectionStringLocalDEv, settings.Password);


        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException("RedisSetting:ConnectionString is required.");
            }

            var opt = new ConfigurationOptions
            {
                AbortOnConnectFail = settings.AbortOnConnectFail,
                ConnectRetry = settings.ConnectRetry,
                ConnectTimeout = settings.ConnectTimeout,
                SyncTimeout = settings.SyncTimeOut,
                // Render internal network does not require TLS
                Ssl = false
            };

            if (!string.IsNullOrEmpty(settings.Password))
            {
                opt.Password = settings.Password;
            }

            opt.EndPoints.Add(endpoint);

            return ConnectionMultiplexer.Connect(opt);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            var cacheOpts = new ConfigurationOptions
            {
                AbortOnConnectFail = settings.AbortOnConnectFail,
                ConnectRetry = settings.ConnectRetry,
                ConnectTimeout = settings.ConnectTimeout,
                SyncTimeout = settings.SyncTimeOut,
                Ssl = false
            };

            cacheOpts.EndPoints.Add(endpoint);

            if (!string.IsNullOrEmpty(password))
            {
                cacheOpts.Password = password;
            }

            options.ConfigurationOptions = cacheOpts;
        });

        return services;
    }

    public static (string endpoint, string password) NormalizeRedis(string cs, string configuredPassword)
    {
        if (string.IsNullOrWhiteSpace(cs))
        {
            throw new InvalidOperationException("RedisSetting:ConnectionString is required.");
        }

        if (cs.StartsWith("redis://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(cs);
            var host = uri.Host;
            var port = uri.IsDefaultPort ? 6379 : uri.Port;

            // user:pass (Render uses a generated token as "password")
            var parts = uri.UserInfo.Split(':', 2);
            var pwd = parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : configuredPassword;

            return ($"{host}:{port}", pwd ?? "");
        }

        // Already host:port or full option string
        return (cs, configuredPassword ?? "");
    }

    private static bool IsRunningInContainer() =>
        string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true", StringComparison.OrdinalIgnoreCase);
}
