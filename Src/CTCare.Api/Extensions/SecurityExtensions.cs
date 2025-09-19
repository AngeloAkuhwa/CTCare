using System.Security.Claims;
using System.Text;

using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Settings;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace CTCare.Api.Extensions;

public static class SecurityExtensions
{
    private const string SchemeCombined = "Combined";
    private const string HeaderName = "x-api-key";
    private const string SchemeBearer = JwtBearerDefaults.AuthenticationScheme;
    private const string SchemeApiKey = ApiKeyAuthOptions.DefaultScheme;
    public const string PolicyJwtAndApi = "JwtAndApiKey";

    /// <summary>
    /// Registers Authentication (JWT + API Key) and Authorization (policy requiring BOTH).
    /// Also registers Swagger security definitions.
    /// </summary>
    public static IServiceCollection AddAppSecurity(this IServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        var cfg = sp.GetRequiredService<IConfiguration>();

        // Bind JwtSettings into DI
        var jwtSettings = new JwtSettings();
        cfg.GetSection(nameof(JwtSettings)).Bind(jwtSettings);

        //Auth specific services registration
        services.AddSingleton(jwtSettings);
        services.AddScoped<IAuthenticationHandler, ApiKeyAuthHandler>();
        services.AddScoped<IAuthorizationHandler, BothJwtAndApiKeyHandler>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = SchemeCombined;
                options.DefaultChallengeScheme = SchemeCombined;
            })
            .AddJwtBearer(SchemeBearer, o =>
            {               

                o.RequireHttpsMetadata = true;
                o.SaveToken = true;

                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(jwtSettings.Issuer),
                    ValidIssuer = jwtSettings.Issuer,

                    ValidateAudience = !string.IsNullOrWhiteSpace(jwtSettings.Audience),
                    ValidAudience = jwtSettings.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret ?? string.Empty)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // Allow header or query fallback for websockets/hubs if ever needed
                        if (string.IsNullOrEmpty(ctx.Token) && ctx.Request.Query.TryGetValue("access_token", out var at))
                        {
                            ctx.Token = at.ToString();
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ctx =>
                    {
                        // enrich the identity here (e.g., map roles/claims from DB)
                        // e.g., heartbeat claim
                        var id = (ClaimsIdentity)ctx.Principal!.Identity!;
                        id.AddClaim(new Claim("token_validated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));
                        var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JWT");
                        log.LogInformation("JWT OK: sub={sub}, aud={aud}, iss={iss}",
                            ctx.Principal?.FindFirst("sub")?.Value,
                            ctx.Principal?.FindFirst("aud")?.Value,
                            ctx.Principal?.FindFirst("iss")?.Value);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = ctx =>
                    {
                        ctx.Response.Headers.Append("X-Auth-Error", "jwt_auth_failed");
                        var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JWT");
                        log.LogWarning(ctx.Exception, "JWT failed. Authorization='{Authorization}'", ctx.Request.Headers.Authorization.ToString());
                        return Task.CompletedTask;
                    },
                    OnChallenge = ctx =>
                    {
                        // Keep default challenge behavior, but surface diagnostics
                        ctx.Response.Headers.Append("WWW-Authenticate", "Bearer");
                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<ApiKeyAuthOptions, ApiKeyAuthHandler>(SchemeApiKey, _ => { })
            // Selector that routes to a scheme, but the policy below enforces BOTH succeed
            .AddPolicyScheme(SchemeCombined, "JWT+APIKey selector", o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    var hasApi = ctx.Request.Headers.ContainsKey(ApiKeyAuthOptions.HeaderName);
                    var hasAuth = ctx.Request.Headers.ContainsKey("Authorization");

                    // We pick one to perform the initial authenticate challenge;
                    // Authorization policy later ensures BOTH are valid.
                    if (hasAuth)
                    {
                        return SchemeBearer;
                    }

                    if (hasApi)
                    {
                        return SchemeApiKey;
                    }

                    return SchemeBearer; // default so browser swagger works (then policy denies)
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(PolicyJwtAndApi, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new BothJwtAndApiKeyRequirement());
            });
        });

        return services;
    }
}
