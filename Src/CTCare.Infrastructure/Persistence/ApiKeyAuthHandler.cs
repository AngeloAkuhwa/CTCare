using System.Security.Claims;
using System.Text.Encodings.Web;

using CTCare.Infrastructure.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Persistence;

public class ApiKeyAuthOptions: AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string HeaderName = "x-api-key";
}

public class ApiKeyAuthHandler(
    IOptionsMonitor<ApiKeyAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISystemClock clock,
    CtCareDbContext db)
    : AuthenticationHandler<ApiKeyAuthOptions>(options, logger, encoder, clock)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No header? Just say "no result" so other schemes e.g., JWT, can try.
        if (!Request.Headers.TryGetValue(ApiKeyAuthOptions.HeaderName, out var provided))
        {
            return AuthenticateResult.NoResult();
        }

        var key = provided.ToString().Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return AuthenticateResult.Fail("Empty API key.");
        }

        var prefix = ApiKeyUtilities.GetPrefix(key);

        var rec = await db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Prefix == prefix && !x.IsDeleted);

        if (rec is null || !ApiKeyUtilities.Verify(key, rec.Hash))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        // Build principal
        var claims = new[]
        {
            new Claim("api_key_prefix", rec.Prefix),
            new Claim(ClaimTypes.Name,  rec.Name ?? $"api:{rec.Prefix}"),
            new Claim(ClaimTypes.Role,  "ApiClient")
        };

        var id = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(id), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
