namespace CTCare.Api.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCorsOpenPolicy(this IServiceCollection services, string policyName)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(policyName, p =>
                p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });
        return services;
    }
}
