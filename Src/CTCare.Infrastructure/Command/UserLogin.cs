using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Application.Notification;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class UserLogin
{
    public class Command: IRequest<Result>
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, PasswordPropertyText]
        public string Password { get; set; }

        [Required]
        public string? Ip { get; set; }

        [Required]
        public string? UserAgent { get; set; }

        public bool UseOtp { get; set; }
    }

    public class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public string AccessToken { get; init; } = "";
        public DateTimeOffset AccessTokenExpiresAt { get; init; }

        public string RefreshToken { get; init; } = "";
        public DateTimeOffset RefreshTokenExpiresAt { get; init; }

        public bool OtpRequired { get; init; }
        public string? Delivery { get; init; }
    }


    public class Handler(
        CtCareDbContext db,
        IPasswordHasher hasher,
        IOtpService otp,
        IEmailService email,
        ILoginAttemptService attempt,
        IHttpContextAccessor http,
        ILogger<Handler> log,
        IOptions<AuthSettings> authOpt ,
        IJwtTokenService jwt,
        IRoleResolver rolesResolver,
        IRefreshTokenService refreshTokens
    ): IRequestHandler<Command, Result>
    {
        private const string LockedMsgTemplate = "Too many failed attempts. Try again in {0} minutes.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            var emailNorm = (req.Email ?? "").Trim().ToLowerInvariant();
            var ip = !string.IsNullOrWhiteSpace(req.Ip) ? req.Ip! : (http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            using var scope = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = "login",
                ["email"] = emailNorm,
                ["ip"] = ip,
                ["ua"] = req.UserAgent
            });

            var (failKey, lockKey) = (CacheKeys.Email_Ip_FailKey(emailNorm, ip), CacheKeys.Email_Ip_LockKey(emailNorm, ip));
            var status = await attempt.CheckStatusAsync(failKey, lockKey, ct);
            if (status.IsLocked)
            {
                var minutes = (int)(status.RemainingLockout?.TotalMinutes ?? 0);
                return new Result(HttpStatusCode.Forbidden) { ErrorMessage = string.Format(LockedMsgTemplate, minutes) };
            }

            var account = await db.UserAccounts
                .Include(u => u.Employee).ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(u => u.Email == emailNorm, ct);

            if (account is null)
            {
                await attempt.RegisterFailureAsync(failKey, lockKey, ct);
                return Unauthorized();
            }

            if (account.LockoutEndUtc is { } until && until > DateTimeOffset.UtcNow)
            {
                return Unauthorized("Account locked. Try again later.");
            }

            if (!hasher.Verify(req.Password ?? string.Empty, account.PasswordHash, account.PasswordSalt))
            {
                var res = await attempt.RegisterFailureAsync(failKey, lockKey, ct);
                if (res.IsLocked)
                {
                    var minutes = (int)(res.RemainingLockout?.TotalMinutes ?? 0);
                    return new Result(HttpStatusCode.Forbidden) { ErrorMessage = string.Format(LockedMsgTemplate, minutes) };
                }

                account.AccessFailedCount++;
                if (account.AccessFailedCount >= 5)
                {
                    account.LockoutEndUtc = DateTimeOffset.UtcNow.AddMinutes(10);
                    account.AccessFailedCount = 0;
                }

                await db.SaveChangesAsync(ct);

                return Unauthorized();
            }

            await attempt.ResetAsync(ct, failKey, lockKey);
            account.AccessFailedCount = 0;
            account.LockoutEndUtc = null;

            if (!account.EmailConfirmed || account.Employee.EmailStatus != EmailStatus.Verified)
            {
                await db.SaveChangesAsync(ct);
                return new Result(HttpStatusCode.Forbidden) { ErrorMessage = "Email not confirmed." };
            }
            if (account.Employee.Status is EmploymentStatus.Terminated or EmploymentStatus.Suspended)
            {
                await db.SaveChangesAsync(ct);
                return new Result(HttpStatusCode.Forbidden) { ErrorMessage = "Account is not active." };
            }

            if (account.TwoFactorEnabled || req.UseOtp)
            {
                var code = await otp.IssueLoginOtpAsync(account.Email, ip, ct);
                await email.SendEmailAsync(account.Email, "Your CTCare sign-in code",
                    $"Your one-time code is: <strong>{code}</strong>. It expires in 10 minutes.", ct: ct);
                account.LastLoginIp = ip;
                await db.SaveChangesAsync(ct);
                return new Result(HttpStatusCode.Accepted) { OtpRequired = true, Delivery = "Email" };
            }

            var roles = rolesResolver.Resolve(account.Employee);
            var (access, expAt) = jwt.IssueAccessToken(account, roles);
            var rt =  refreshTokens.Issue(account.EmployeeId.Value, authOpt.Value.RefreshTokenValidityDays,db, ct);

            db.RefreshTokens.Add(rt);
            account.LastLoginAtUtc = DateTimeOffset.UtcNow;
            account.LastLoginIp = ip;
            await refreshTokens.TrimActiveAsync(account.EmployeeId.Value, keep: 5, db, ct);
            await db.SaveChangesAsync(ct);

            return new Result(HttpStatusCode.OK)
            {
                AccessToken = access,
                AccessTokenExpiresAt = expAt,
                RefreshToken = rt.Token,
                RefreshTokenExpiresAt = rt.ExpiresAt
            };
        }

        private static Result Unauthorized(string? msg = null) => new(HttpStatusCode.Unauthorized) { ErrorMessage = msg ?? "Invalid credentials." };
    }
}
