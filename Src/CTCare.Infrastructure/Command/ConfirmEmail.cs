using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;

using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Command;

// TODO: clean up hardcoded string messages
public static class ConfirmEmail
{
    public sealed class Command: IRequest<Result>
    {
        [Required]
        public string Token { get; set; }

        [EmailAddress]
        public string? Email { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public bool Confirmed { get; init; }

        public bool AlreadyConfirmed { get; init; }
    }

    public sealed class Handler(CtCareDbContext db, ILogger<Handler> log): IRequestHandler<Command, Result>
    {
        private const string OpName = "confirm_email";
        private const string ConfirmPrefix = "confirm:";
        private const int MaxTokenLength = 512;
        private static readonly Regex Base64Url = new("^[A-Za-z0-9_-]{16,128}$", RegexOptions.Compiled);

        private const string MsgTokenRequired = "Token is required.";
        private const string MsgInvalidOrExpired = "Invalid or expired confirmation token.";
        private const string MsgInvalidContext = "Invalid token context.";
        private const string MsgEmailMismatch = "Email does not match token.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            var raw = (req.Token ?? string.Empty).Trim();
            var emailNorm = req.Email?.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(raw))
            {
                return BadRequest(MsgTokenRequired);
            }

            if (raw.Length > MaxTokenLength)
            {
                return BadRequest(MsgInvalidOrExpired);
            }

            var token = raw.StartsWith(ConfirmPrefix, StringComparison.OrdinalIgnoreCase) ? raw : $"{ConfirmPrefix}{raw}";
            var core = token.StartsWith(ConfirmPrefix, StringComparison.OrdinalIgnoreCase) ? token[ConfirmPrefix.Length..] : token;

            if (!Base64Url.IsMatch(core))
            {
                return BadRequest(MsgInvalidOrExpired);
            }

            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["email"] = emailNorm,
                ["token_masked"] = Mask(token)
            });

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            try
            {
                var now = DateTimeOffset.UtcNow;
                var confirm = await db.RefreshTokens
                    .AsTracking()
                    .FirstOrDefaultAsync(t => t.Token == token && !t.Revoked && t.ExpiresAt > now, ct);

                if (confirm is null || confirm.Revoked || confirm.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    log.LogWarning("Confirm token invalid or expired.");
                    return BadRequest(MsgInvalidOrExpired);
                }

                var account = await db.UserAccounts
                    .Include(u => u.Employee)
                    .FirstOrDefaultAsync(u => u.EmployeeId == confirm.EmployeeId, ct);

                if (account is null)
                {
                    log.LogWarning("Confirm token has no associated account.");
                    return BadRequest(MsgInvalidContext);
                }

                if (!string.IsNullOrWhiteSpace(emailNorm) &&
                    !string.Equals(account.Email, emailNorm, StringComparison.Ordinal))
                {
                    log.LogWarning("Email mismatch during confirmation.");
                    return BadRequest(MsgEmailMismatch);
                }

                if (account is { EmailConfirmed: true, Employee.EmailStatus: EmailStatus.Verified })
                {
                    confirm.Revoked = true;
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);

                    log.LogInformation("Email already confirmed; token revoked.");
                    return new Result(HttpStatusCode.OK)
                    {
                        Confirmed = true,
                        AlreadyConfirmed = true
                    };
                }

                account.EmailConfirmed = true;
                account.Employee.EmailStatus = EmailStatus.Verified;

                confirm.Revoked = true;

                await db.RefreshTokens
                    .Where(t => t.EmployeeId == account.EmployeeId
                                && !t.Revoked
                                && t.ExpiresAt > now
                                && t.Token.StartsWith(ConfirmPrefix))
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.Revoked, true), ct);

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                log.LogInformation("Email confirmed successfully.");
                return new Result(HttpStatusCode.OK)
                {
                    Confirmed = true,
                    AlreadyConfirmed = false
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                log.LogError(ex, "Confirm email failed.");
                return new Result(HttpStatusCode.InternalServerError) { ErrorMessage = "Could not confirm email at this time." };
            }
        }

        private static Result BadRequest(string message) => new(HttpStatusCode.BadRequest) { ErrorMessage = message };

        private static string Mask(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }

            const int keep = 3;
            var core = token.StartsWith(ConfirmPrefix, StringComparison.OrdinalIgnoreCase)
                ? token.Substring(ConfirmPrefix.Length)
                : token;
            if (core.Length <= keep * 2)
            {
                return $"{ConfirmPrefix}{core}";
            }

            return $"{ConfirmPrefix}{core[..keep]}...{core[^keep..]}";
        }
    }
}
