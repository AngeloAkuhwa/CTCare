namespace CTCare.Api.Extensions;

public sealed record ApiKeyOptions(string[] Keys);

public static class ApiKeyExtensions
{
    public static IServiceCollection AddApiKeyAuthOptions(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var keys = cfg.GetSection("Auth:ApiKeys").Get<string[]>() ?? Array.Empty<string>();
        services.AddSingleton(new ApiKeyOptions(keys));
        return services;
    }

    public static IApplicationBuilder UseApiKeyGate(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger") || path.StartsWith("/health") || path.StartsWith("/hangfire"))
            {
                await next();
                return;
            }

            var opts = ctx.RequestServices.GetRequiredService<ApiKeyOptions>();

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
