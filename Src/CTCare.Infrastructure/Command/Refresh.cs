using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;

using CTCare.Application.Interfaces;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class Refresh
{
    public class Command: IRequest<Result>
    {
        [Required] public string RefreshToken { get; set; }

        [Required]
        public string AccessToken { get; set; }

        [Required]
        public string? Ip { get; set; }

        [Required]
        public string? UserAgent { get; set; }

        public bool RevokeAllSessions { get; set; }
    }

    public class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public string AccessToken { get; init; }
        public DateTimeOffset AccessTokenExpiresAt { get; init; }

        public string RefreshToken { get; init; }
        public DateTimeOffset RefreshTokenExpiresAt { get; init; }
    }

    public class Handler(
        CtCareDbContext db,
        IJwtTokenService jwt,
        IOptions<AuthSettings> authOpt,
        ILogger<Handler> log,
        IRoleResolver rolesResolver,
        IRefreshTokenService refreshTokens
    ): IRequestHandler<Command, Result>
    {
        private const string OpName = "refresh_token";
        private const string MsgAccessParseInfo = "Access token parsing skipped/failed; proceeding with refresh token only.";
        private const string MsgRotationFailed = "Refresh token rotation failed.";
        private const string MsgCouldNotRefresh = "Could not refresh session.";
        private const string MsgBindingFailed = "Refresh binding failed: access token email != account email.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            using var scope = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["ip"] = req.Ip,
                ["ua"] = req.UserAgent
            });

            var tokenRaw = (req.RefreshToken ?? "").Trim();

            if (string.IsNullOrWhiteSpace(tokenRaw))
            {
                return new Result(HttpStatusCode.BadRequest);
            }

            string? emailFromAccess = null;
            if (!string.IsNullOrWhiteSpace(req.AccessToken))
            {
                try
                {
                    var principal = jwt.GetPrincipalFromExpiredToken(req.AccessToken);
                    emailFromAccess = principal.FindFirst(ClaimTypes.Email)?.Value?.Trim().ToLowerInvariant();
                }
                catch (Exception ex)
                {
                    // Do not fail the request solely because the client did not send/parse an access token.
                    log.LogInformation(ex, MsgAccessParseInfo);
                    return new Result(HttpStatusCode.BadRequest);
                }
            }

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            try
            {
                var now = DateTimeOffset.UtcNow;
                var rt = await db.RefreshTokens
                    .AsTracking()
                    .FirstOrDefaultAsync(r => r.Token == tokenRaw, ct);

                if (rt is null || rt.Revoked || rt.ExpiresAt <= now)
                {
                    return Unauthorized();
                }

                if (rt.Revoked || rt.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    return Unauthorized();
                }

                var account = await db.UserAccounts
                    .Include(u => u.Employee).ThenInclude(e => e.Department)
                    .FirstOrDefaultAsync(u => u.EmployeeId == rt.EmployeeId, ct);

                if (account is null)
                {
                    return Unauthorized();
                }

                if (!string.IsNullOrWhiteSpace(emailFromAccess) && !string.Equals(emailFromAccess, account.Email, StringComparison.Ordinal))
                {
                    // Suspicious: refresh token does not match the access token's identity
                    log.LogWarning(MsgBindingFailed);
                    return Unauthorized();
                }

                if (!account.EmailConfirmed || account.Employee.EmailStatus != EmailStatus.Verified)
                {
                    return new Result(HttpStatusCode.Forbidden);
                }

                if (account.Employee.Status is EmploymentStatus.Terminated or EmploymentStatus.Suspended)
                {
                    return new Result(HttpStatusCode.Forbidden);
                }

                if (req.RevokeAllSessions)
                {
                    await refreshTokens.RevokeAllActiveAsync(account.EmployeeId.Value,db, ct);
                }
                else
                {
                    await refreshTokens.RevokeAsync(req.AccessToken!,db, ct);
                }

                var roles = rolesResolver.Resolve(account.Employee);
                var (access, accessExp) = jwt.IssueAccessToken(account, roles);

                var newRt = refreshTokens.Issue(account.EmployeeId.Value, authOpt.Value.RefreshTokenValidityDays,db, ct);

                db.RefreshTokens.Add(newRt);

                await refreshTokens.TrimActiveAsync(account.EmployeeId.Value, keep: 5, db, ct);

                account.LastLoginAtUtc = DateTimeOffset.UtcNow;
                account.LastLoginIp = req.Ip;

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new Result(HttpStatusCode.OK)
                {
                    AccessToken = access,
                    AccessTokenExpiresAt = accessExp,
                    RefreshToken = newRt.Token,
                    RefreshTokenExpiresAt = newRt.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, MsgRotationFailed);
                return new Result(HttpStatusCode.InternalServerError) { ErrorMessage = MsgCouldNotRefresh };
            }
        }

        private static Result Unauthorized(string? msg = null)
            => new(HttpStatusCode.Unauthorized) { ErrorMessage = msg ?? "Unauthorized." };
    }
}
