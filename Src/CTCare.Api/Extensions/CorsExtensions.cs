using CTCare.Shared.Settings;

namespace CTCare.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCorsOpenPolicy(this IServiceCollection services, IConfiguration config, CorsSettings settings)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(settings.PolicyName, p =>
                p.WithOrigins(settings.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(settings.PreflightMaxAgeMinutes)));
        });
        return services;
    }
}
