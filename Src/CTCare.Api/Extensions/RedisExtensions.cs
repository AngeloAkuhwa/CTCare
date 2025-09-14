using CTCare.Api.Extensions.Utility;
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

        var (endpoint, password) = Helper.NormalizeRedis(
            env.IsProduction() || Helper.IsRunningInContainer()
                ? settings.ConnectionString
                : settings.ConnectionStringLocalDEv,
            settings.Password);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var opt = new ConfigurationOptions
            {
                AbortOnConnectFail = settings.AbortOnConnectFail,
                ConnectRetry = settings.ConnectRetry,
                ConnectTimeout = settings.ConnectTimeout,
                SyncTimeout = settings.SyncTimeOut,
                Ssl = false, // internal network  => false
            };

            opt.EndPoints.Add(endpoint);

            if (!string.IsNullOrEmpty(password))
            {
                opt.Password = password;
            }

            return ConnectionMultiplexer.Connect(opt);
        });

        services.AddStackExchangeRedisCache(o =>
        {
            var co = new ConfigurationOptions
            {
                AbortOnConnectFail = settings.AbortOnConnectFail,
                ConnectRetry = settings.ConnectRetry,
                ConnectTimeout = settings.ConnectTimeout,
                SyncTimeout = settings.SyncTimeOut,
                Ssl = false
            };

            co.EndPoints.Add(endpoint);
            if (!string.IsNullOrEmpty(password))
            {
                co.Password = password;
            }

            o.ConfigurationOptions = co;
        });

        return services;
    }
}
