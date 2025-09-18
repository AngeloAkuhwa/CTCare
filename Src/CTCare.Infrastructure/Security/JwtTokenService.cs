using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using CTCare.Domain.Entities;
using CTCare.Shared.Settings;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CTCare.Infrastructure.Security;

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) IssueAccessToken(User user, string[] roles);
    ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
}

public sealed class JwtTokenService(IOptions<JwtSettings> opt, IHttpContextAccessor httpContextAccessor): IJwtTokenService
{
    public (string AccessToken, DateTimeOffset ExpiresAt) IssueAccessToken(User user, string[] roles)
    {
        var http = httpContextAccessor.HttpContext;
        var ip = http?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = http?.Request?.Headers["User-Agent"].ToString() ?? "unknown";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Value.Secret));
        var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var subjectId = (user.EmployeeId == Guid.Empty ? user.Id : user.EmployeeId).ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId),
            new(ClaimTypes.NameIdentifier, subjectId),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.Employee?.EmployeeCode ?? user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("ip", ip),
            new("ua", ua),
            new("dept_code", user.Employee?.Department?.Code ?? "")
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(opt.Value.ExpiryMinutes);

        var jwt = new JwtSecurityToken(
            issuer: opt.Value.Issuer,
            audience: opt.Value.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: cred);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring("Bearer ".Length).Trim();
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opt.Value.Secret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, tokenValidationParameters, out _);
        return principal;
    }
}
