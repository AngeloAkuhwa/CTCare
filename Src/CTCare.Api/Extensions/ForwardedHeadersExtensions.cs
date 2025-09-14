using Microsoft.AspNetCore.HttpOverrides;

namespace CTCare.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Trust the headers added by proxy/load balancer
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Clear defaults and trust *all* networks (Render will handle security)
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }
}
