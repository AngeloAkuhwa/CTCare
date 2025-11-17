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

        services.AddOptions<RedisSetting>()
            .Bind(cfg.GetSection("RedisSetting"))
            .ValidateOnStart();

        var settings = cfg.GetSection("RedisSetting").Get<RedisSetting>() ?? new();

        var (endpoint, password) =
            (env.IsProduction() || Helper.IsRunningInContainer())
                ? Helper.NormalizeRedis(settings.ConnectionString, settings.Password)
                : (settings.ConnectionStringLocalDEv, "");

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var coMux = ConfigurationOptions.Parse(endpoint, true);
            coMux.AbortOnConnectFail = settings.AbortOnConnectFail;
            coMux.ConnectRetry = settings.ConnectRetry;
            coMux.ConnectTimeout = settings.ConnectTimeout;
            coMux.SyncTimeout = settings.SyncTimeOut;
            coMux.Ssl = false;

            if (!string.IsNullOrWhiteSpace(password))
            {
                coMux.Password = password;
            }

            return ConnectionMultiplexer.Connect(coMux);
        });

        // TODO: remove this and prefer Multiplexer
        services.AddStackExchangeRedisCache(o =>
        {
            var co = ConfigurationOptions.Parse(endpoint,  false);
            co.AbortOnConnectFail = settings.AbortOnConnectFail;
            co.ConnectRetry = settings.ConnectRetry;
            co.ConnectTimeout = settings.ConnectTimeout;
            co.SyncTimeout = settings.SyncTimeOut;
            co.Ssl = false;
            if (!string.IsNullOrWhiteSpace(password))
            {
                co.Password = password;
            }

            o.ConfigurationOptions = co;
            o.InstanceName = "ctcare:";
        });

        return services;
    }
}
