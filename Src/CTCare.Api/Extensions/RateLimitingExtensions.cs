using System.Threading.RateLimiting;

namespace CTCare.Api.Extensions;

public sealed class RateLimiting
{
    public int RequestsPerMinute { get; set; }
    public int Burst { get; set; }
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddGlobalRateLimiting(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        var model = new RateLimiting();
        cfg.GetSection("RateLimiting").Bind(model);

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ip,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = model.RequestsPerMinute,
                        QueueLimit = model.Burst,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
