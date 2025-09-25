using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CTCare.Api.Extensions;

public static class ApiKeyExtensions
{
    public sealed class ApiKeyOptions
    {
        public string[] Keys { get; init; } = Array.Empty<string>();
        public string[] KeylessPaths { get; init; } = Array.Empty<string>();
    }

    public static IServiceCollection AddApiKeyAuthOptions(this IServiceCollection services, IConfiguration cfg)
    {
        services.Configure<ApiKeyOptions>(cfg.GetSection("Api"));
        return services;
    }

    // IMPORTANT: This middleware must run AFTER UseRouting so endpoint metadata is available
    public static IApplicationBuilder UseApiKeyGate(this IApplicationBuilder app, IConfiguration cfg, IWebHostEnvironment env)
    {
        return app.Use(async (ctx, next) =>
        {
            if (HttpMethods.IsOptions(ctx.Request.Method))
            {
                await next();
                return;
            }

            // Allow endpoints marked [AllowAnonymous]
            var endpoint = ctx.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null)
            {
                await next();
                return;
            }

            var opts = ctx.RequestServices.GetRequiredService<IOptions<ApiKeyOptions>>().Value;
            var path = ctx.Request.Path.Value ?? string.Empty;

            // Whitelist prefixes
            if (opts.KeylessPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                await next();
                return;
            }

            // Dev-only convenience: allow ?api_key=... for quick manual tests
            if (env.IsDevelopment() &&
                ctx.Request.Query.TryGetValue("api_key", out var qk) &&
                opts.Keys.Contains(qk.ToString()))
            {
                await next();
                return;
            }

            if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
                !opts.Keys.Contains(provided.ToString()))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsync("Missing or invalid API key.");
                return;
            }

            await next();
        });
    }
}
