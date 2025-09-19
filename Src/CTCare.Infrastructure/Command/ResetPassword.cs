using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Shared.BasicResult;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Command;

public static class ResetPassword
{
    public sealed class Command: IRequest<Result>
    {
        [Required] public string Token { get; set; }

        [Required, MinLength(8)]
        public string NewPassword { get; set; }

        [Required, Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }
        public bool ResetDone { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IPasswordHasher hasher,
        ILogger<Handler> log,
        IRefreshTokenService refreshToken
    ): IRequestHandler<Command, Result>
    {
        private const string OpName = "reset_password";
        private const string ResetPrefix = "reset:";

        private const string MsgTokenRequired = "Reset token is required.";
        private const string MsgInvalidToken = "Invalid or expired reset token.";
        private const string MsgBadPassword = "Password does not meet requirements.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            var raw = (req.Token ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return BadRequest(MsgTokenRequired);
            }

            var token = raw.StartsWith(ResetPrefix, StringComparison.OrdinalIgnoreCase) ? raw : $"{ResetPrefix}{raw}";
            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["token_masked"] = Mask(token)
            });

            if (!MeetsPasswordPolicy(req.NewPassword, out var reason))
            {
                return BadRequest(MsgBadPassword);
            }
            var rawAccess = raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? raw["Bearer ".Length..].Trim()
                : raw.Trim();

            var handler = new JwtSecurityTokenHandler();
            var parsed = handler.ReadJwtToken(rawAccess);

            var empIdFromAccess = parsed.Claims.FirstOrDefault(c => c.Type is ClaimTypes.NameIdentifier or "empId")?.Value;

            if (parsed.ValidTo <= DateTime.UtcNow)
            {
                if (!Guid.TryParse(empIdFromAccess, out var empId))
                {
                    return Unauthorized();
                }

                await refreshToken.RevokeAllActiveAsync(empId, db, ct);
                await db.SaveChangesAsync(ct);

                return Unauthorized();
            }

            var now = DateTimeOffset.UtcNow;

            var reset = await db.RefreshTokens
                .AsTracking()
                .FirstOrDefaultAsync(t =>
                    t.Token == token &&
                    !t.Revoked &&
                    t.ExpiresAt > now, ct);

            if (reset is null)
            {
                log.LogWarning("Reset token invalid/expired.");
                return Unauthorized(MsgInvalidToken);
            }

            var account = await db.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeId == reset.EmployeeId, ct);

            if (account is null)
            {
                log.LogWarning("Reset token has no associated account.");
                return BadRequest(MsgInvalidToken);
            }

            if (account.EmployeeId == Guid.Empty)
            {
                log.LogWarning("Account state invalid for reset.");
                return BadRequest(MsgInvalidToken);
            }

            var (hash, salt) = hasher.Hash(req.NewPassword);
            account.PasswordHash = hash;
            account.PasswordSalt = salt;

            // Reset lockout counters (fresh start after reset)
            account.AccessFailedCount = 0;
            account.LockoutEndUtc = null;
            reset.Revoked = true;

            var siblings = await db.RefreshTokens
                .Where(t => t.EmployeeId == account.EmployeeId
                            && !t.Revoked
                            && t.ExpiresAt > now
                            && t.Token.StartsWith(ResetPrefix))
                .ToListAsync(ct);

            foreach (var s in siblings)
            {
                s.Revoked = true;
            }

            // RevokeAsync ALL active refresh tokens to log out other sessions
            var activeRefresh = await db.RefreshTokens
                .Where(t => t.EmployeeId == account.EmployeeId
                            && !t.Revoked
                            && t.ExpiresAt > now
                            && !t.Token.StartsWith(ResetPrefix)
                            && !t.Token.StartsWith("confirm:", StringComparison.Ordinal))
                .ToListAsync(ct);

            foreach (var r in activeRefresh)
            {
                r.Revoked = true;
            }

            await db.SaveChangesAsync(ct);

            log.LogInformation("Password reset successful; sessions revoked.");
            return new Result(HttpStatusCode.OK) { ResetDone = true };
        }

        private static Result BadRequest(string message) => new(HttpStatusCode.BadRequest) { ErrorMessage = message };
        private static Result Unauthorized(string? msg = null) => new(HttpStatusCode.Unauthorized) { ErrorMessage = msg ?? "Invalid credentials." };

        private static string Mask(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }

            const int keep = 3;
            var core = token.StartsWith(ResetPrefix, StringComparison.OrdinalIgnoreCase)? token.Substring(ResetPrefix.Length) : token;
            return core.Length <= keep * 2 ? $"{ResetPrefix}{core}" : $"{ResetPrefix}{core[..keep]}...{core[^keep..]}";
        }

        // Simple server-side policy gate; expand if you add a dedicated validator later
        // Global-standard-ish password policy:
        // Length: 12–128 (NIST recommends >= 8; many orgs require 12+)
        // Avoid long repeats and straight sequences
        // Composition: require at least 3 of 4 classes (upper/lower/digit/symbol)
        private static bool MeetsPasswordPolicy(string pwd, out string? reason, string? email = "")
        {
            reason = null;

            if (string.IsNullOrWhiteSpace(pwd))
            {
                reason = "Password is required.";
                return false;
            }

            // Normalize to reduce weirdness with unicode equivalence
            var normalized = pwd.Normalize(NormalizationForm.FormKC);

            const int minLen = 12;
            const int maxLen = 128;
            if (normalized.Length < minLen)
            {
                reason = $"Password must be at least {minLen} characters long.";
                return false;
            }
            if (normalized.Length > maxLen)
            {
                reason = $"Password must be at most {maxLen} characters long.";
                return false;
            }

            var lower = normalized.ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(email))
            {
                var local = email.Split('@')[0].Trim().ToLowerInvariant();
                if (local.Length >= 4 && lower.Contains(local))
                {
                    reason = "Password must not contain your email/username.";
                    return false;
                }
            }

            var hasLower = normalized.Any(char.IsLower);
            var hasUpper = normalized.Any(char.IsUpper);
            var hasDigit = normalized.Any(char.IsDigit);
            var hasSymbol = normalized.Any(ch => char.IsPunctuation(ch) || char.IsSymbol(ch));
            var classes = (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
            if (classes < 3)
            {
                reason = "Use at least three of: uppercase, lowercase, digit, symbol.";
                return false;
            }

            // Too many repeated chars (e.g., aaaa, 1111)
            if (HasRepeatedRun(normalized, 4))
            {
                reason = "Avoid long runs of the same character.";
                return false;
            }

            // Straight sequences (e.g., abcd, 1234)
            if (HasSequentialAlNum(normalized, 4))
            {
                reason = "Avoid long sequential character patterns.";
                return false;
            }

            return true;
        }

        private static bool HasRepeatedRun(string s, int run)
        {
            var count = 1;
            for (var i = 1; i < s.Length; i++)
            {
                if (s[i] == s[i - 1])
                {
                    count++;
                    if (count >= run)
                    {
                        return true;
                    }
                }
                else
                {
                    count = 1;
                }
            }
            return false;
        }

        private static bool HasSequentialAlNum(string s, int len)
        {
            var count = 1;
            for (var i = 1; i < s.Length; i++)
            {
                if (IsNextChar(s[i - 1], s[i]))
                {
                    count++;
                    if (count >= len)
                    {
                        return true;
                    }
                }
                else
                {
                    count = 1;
                }
            }
            return false;
        }

        private static bool IsNextChar(char a, char b)
        {
            if (char.IsLetter(a) && char.IsLetter(b))
            {
                return (char.ToLowerInvariant(b) - char.ToLowerInvariant(a)) == 1;
            }

            if (char.IsDigit(a) && char.IsDigit(b))
            {
                return (b - a) == 1;
            }

            return false;
        }
    }
}
