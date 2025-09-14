using CTCare.Shared.Settings;

using StackExchange.Redis;

namespace CTCare.Api.Extensions;

public static class RedisExtensions
{
    public static IServiceCollection AddRedisCaching(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var settings = new RedisSetting();
        cfg.GetSection(nameof(RedisSetting)).Bind(settings);

        services.AddSingleton(settings);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException("RedisSetting:ConnectionString is required.");
            }

            var opt = ConfigurationOptions.Parse(settings.ConnectionString, true);
            if (!string.IsNullOrEmpty(settings.Password))
            {
                opt.Password = settings.Password;
            }

            opt.SyncTimeout = settings.SyncTimeOut;
            return ConnectionMultiplexer.Connect(opt);
        });

        services.AddStackExchangeRedisCache(options =>
        {
            if (string.IsNullOrEmpty(settings?.ConnectionString))
            {
                return;
            }

            options.Configuration = settings.ConnectionString;
            options.ConfigurationOptions = new ConfigurationOptions
            {
                AbortOnConnectFail = settings.AbortOnConnectFail,
                ConnectRetry = settings.ConnectRetry,
                ConnectTimeout = settings.ConnectTimeout,
                EndPoints = { settings.ConnectionString },
                Password = settings.Password,
                SyncTimeout = settings.SyncTimeOut
            };
        });

        return services;
    }
}
