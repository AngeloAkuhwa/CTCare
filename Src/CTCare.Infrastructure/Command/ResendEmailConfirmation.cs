using System.ComponentModel.DataAnnotations;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Application.Notification;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Infrastructure.Utilities;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class ResendEmailConfirmation
{
    public sealed class Command: IRequest<Result>
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public bool Dispatched { get; init; }

        public bool AlreadyConfirmed { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IEmailService emailService,
        IOptions<AppSettings> appCfg,
        IOptions<AuthSettings> authCfg,
        ILogger<Handler> log,
        IRefreshTokenService refreshToken,
        ICacheService cache,
        IHttpContextAccessor http
    ): IRequestHandler<Command, Result>
    {
        private const string OpName = "resend_confirm_email";
        private const string ConfirmPrefix = "confirm:";
        private const string EmailTemplate = "Templates.Email.ConfirmAccount.cshtml";
        private const string EmailSubject = "Confirm your CTCare account";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["email"] = email
            });

            // 0) Per-email cooldown to prevent spamming
            var cdKey = CacheKeys.ConfirmEmail_Cooldown(email);
            var onCooldown = await cache.GetAsync(cdKey, ct);
            if (!string.IsNullOrEmpty(onCooldown))
            {
                log.LogInformation("Resend blocked by cooldown.");
                return OkGeneric(dispatched: true, alreadyConfirmed: false);
            }

            // Get account without revealing existence in the API response.
            var account = await db.UserAccounts
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            // If we don't know the account, return OK to avoid enumeration.
            if (account is null)
            {
                log.LogInformation("Resend requested for unknown email; returning generic OK.");
                await SetCooldownAsync(cdKey, ct);
                return OkGeneric(dispatched: true, alreadyConfirmed: false);
            }

            // If already confirmed, do NOT issue a new token; still return OK (idempotent UX).
            if (account is { EmailConfirmed: true, Employee.EmailStatus: EmailStatus.Verified })
            {
                log.LogInformation("Account already confirmed; no new token issued.");
                await SetCooldownAsync(cdKey, ct);
                return OkGeneric(dispatched: true, alreadyConfirmed: true);
            }

            var now = DateTimeOffset.UtcNow;

            // RevokeAsync any outstanding confirmation tokens for this account
            await db.RefreshTokens
                .Where(t => t.EmployeeId == account.EmployeeId
                            && !t.Revoked
                            && t.ExpiresAt > now
                            && t.Token.StartsWith(ConfirmPrefix))
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Revoked, true), ct);

            // Issue a fresh confirm token, raw and prefixed
            var raw = refreshToken.GenerateUrlSafeToken();
            var token = $"{ConfirmPrefix}{raw}";
            var ttl = authCfg.Value.PasswordResetTokenExpiryMinutes;

            db.RefreshTokens.Add(new RefreshToken
            {
                EmployeeId = account.EmployeeId.Value,
                Token = token,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
                Revoked = false
            });
            await db.SaveChangesAsync(ct);

            var authBase = UrlBuilder.BuildAuthBase(http, appCfg);
            var confirmUrl = UrlBuilder.WithQuery(
                UrlBuilder.Combine(authBase, "confirm-email"),
                new Dictionary<string, string?>
                {
                    ["token"] = raw,
                    ["email"] = email
                });

            try
            {
                var html = await emailService.RenderTemplateAsync(EmailTemplate, new
                {
                    Name = $"{account.Employee.FirstName} {account.Employee.LastName}",
                    Url = confirmUrl
                });

                await emailService.SendEmailAsync(email, EmailSubject, html, ct: ct);
                log.LogInformation("Resent confirmation email.");
            }
            catch (Exception ex)
            {
                // Don't fail the request; we still return generic OK to avoid enumeration & UX issues.
                log.LogWarning(ex, "Failed to send confirmation email.");
            }

            // Set cooldown after a successful dispatch attempt
            await SetCooldownAsync(cdKey, ct);

            return OkGeneric(dispatched: true, alreadyConfirmed: false);
        }

        private static Result OkGeneric(bool dispatched, bool alreadyConfirmed) => new(HttpStatusCode.OK) { Dispatched = dispatched, AlreadyConfirmed = alreadyConfirmed };

        private async Task SetCooldownAsync(string key, CancellationToken ct)
        {
            var cd = authCfg.Value.EmailConfirmResendCooldown;
            await cache.SetAsync(key, "1", cd, cd, ct);
        }
    }
}
