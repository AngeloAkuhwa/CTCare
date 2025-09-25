using System.ComponentModel.DataAnnotations;
using System.Net;

using CTCare.Application.Interfaces;
using CTCare.Domain.Entities;
using CTCare.Domain.Enums;
using CTCare.Infrastructure.Persistence;
using CTCare.Infrastructure.Security;
using CTCare.Infrastructure.Utilities;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;
using CTCare.Shared.Utilities;

using MediatR;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class ForgotPassword
{
    public sealed class Command: IRequest<Result>
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public bool Dispatched { get; init; }

        public bool NotEligible { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IEmailService emailService,
        IOptions<AppSettings> appCfg,
        IOptions<AuthSettings> authCfg,
        ILogger<Handler> log,
        IRefreshTokenService refreshToken,
        IHttpContextAccessor http
    ): IRequestHandler<Command, Result>
    {
        private const string OpName = "forgot_password";
        private const string ResetPrefix = "reset:";
        private const string TemplatePath = "Templates.Email.ResetPassword.cshtml";
        private const string EmailSubject = "Reset your CTCare password";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();

            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["email"] = email
            });

            var account = await db.UserAccounts
                .AsNoTracking()
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Email == email, ct);

            if (account is null)
            {
                log.LogInformation("Reset requested for unknown email; returning generic OK.");
                return OkGeneric(dispatched: true, notEligible: false);
            }

            if (!account.EmailConfirmed || account.Employee.EmailStatus != EmailStatus.Verified)
            {
                log.LogInformation("Account not eligible (email not confirmed).");
                return OkGeneric(dispatched: true, notEligible: true);
            }

            if (account.Employee.Status is EmploymentStatus.Terminated or EmploymentStatus.Suspended)
            {
                log.LogInformation("Account not eligible (status={Status}).", account.Employee.Status);
                return OkGeneric(dispatched: true, notEligible: true);
            }

            var now = DateTimeOffset.UtcNow;
            await db.RefreshTokens
                .Where(t => t.EmployeeId == account.EmployeeId
                            && !t.Revoked
                            && t.ExpiresAt > now
                            && t.Token.StartsWith(ResetPrefix))
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Revoked, true), ct);

            var raw = refreshToken.GenerateUrlSafeToken();
            var stored = $"{ResetPrefix}{raw}";
            var ttl = authCfg.Value.PasswordResetTokenExpiryMinutes;

            db.RefreshTokens.Add(new RefreshToken
            {
                Id = SequentialGuid.NewGuid(),
                EmployeeId = account.EmployeeId.Value,
                Token = stored,
                ExpiresAt = now.Add(ttl),
                Revoked = false,
                UpdatedBy = account.Id
            });

            await db.SaveChangesAsync(ct);

            var authBase = UrlBuilder.BuildAuthBase(http, appCfg);
            var resetUrl = UrlBuilder.WithQuery(
                UrlBuilder.Combine(authBase, "reset-password"),
                new Dictionary<string, string?>
                {
                    ["token"] = raw,
                    ["email"] = email
                });

            try
            {
                var name = $"{account.Employee.FirstName} {account.Employee.LastName}";
                string html;

                try
                {
                    html = await emailService.RenderTemplateAsync(TemplatePath, new { Name = name, Url = resetUrl });
                }
                catch (Exception renderEx)
                {
                    log.LogWarning(renderEx, "Reset template render failed; using fallback body.");
                    html = $"<p>Hello {name},</p><p>Use the link below to reset your password:</p><p><a href=\"{resetUrl}\">{resetUrl}</a></p><p>This link expires soon.</p>";
                }

                await emailService.SendEmailAsync(email, EmailSubject, html, ct: ct);
                log.LogInformation("Password reset email dispatched.");
            }
            catch (Exception sendEx)
            {
                log.LogWarning(sendEx, "Failed to dispatch password reset email (continuing with 200).");
            }

            return OkGeneric(dispatched: true, notEligible: false);
        }

        private static Result OkGeneric(bool dispatched, bool notEligible)
            => new(HttpStatusCode.OK) { Dispatched = dispatched, NotEligible = notEligible };
    }
}
