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

        var (endpoint, password) = env.IsProduction() || Helper.IsRunningInContainer() ? Helper.NormalizeRedis(settings.ConnectionString ,settings.Password) : (settings.ConnectionStringLocalDEv, "");

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

            if (env.IsProduction() || Helper.IsRunningInContainer())
            {
                opt.EndPoints.Add(endpoint);
            }
            else
            {
                opt = ConfigurationOptions.Parse(endpoint, true);
                opt.AbortOnConnectFail = settings.AbortOnConnectFail;
                opt.ConnectRetry = settings.ConnectRetry;
                opt.ConnectTimeout = settings.ConnectTimeout;
                opt.SyncTimeout = settings.SyncTimeOut;
                opt.Ssl = false; // internal network  => false
            }

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

            if (env.IsProduction() || Helper.IsRunningInContainer())
            {
                co.EndPoints.Add(endpoint);
            }
            else
            {
                o.Configuration = endpoint;
            }

            if (!string.IsNullOrEmpty(password))
            {
                co.Password = password;
            }

            o.ConfigurationOptions = co;
           //o.InstanceName = "ctcare:";
        });

        return services;
    }
}
