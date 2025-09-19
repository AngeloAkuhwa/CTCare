using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;

using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;
using CTCare.Shared.Settings;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Command;

public static class ValidateResetToken
{
    public sealed class Query: IRequest<Result>
    {
        [Required] public string Token { get; set; }
        public string? Email { get; set; }
        public string? ClientRedirectPath { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }

        public bool Valid { get; init; }
        public string RedirectUrl { get; init; }
    }

    public sealed class Handler(
        CtCareDbContext db,
        IOptions<AppSettings> appCfg,
        ILogger<Handler> log
    ): IRequestHandler<Query, Result>
    {
        private const string OpName = "validate_reset_token";
        private const string ResetPrefix = "reset:";
        private const int MaxTokenLength = 512;
        private static readonly Regex Base64Url = new("^[A-Za-z0-9_-]{16,256}$", RegexOptions.Compiled);

        private const string MsgTokenRequired = "Token is required.";
        private const string MsgInvalidOrExpired = "Invalid or expired reset token.";
        private const string DefaultValidPath = "/reset-password";
        private const string DefaultInvalidPath = "/reset-password/invalid";

        public async Task<Result> Handle(Query req, CancellationToken ct)
        {
            var raw = (req.Token ?? string.Empty).Trim();
            var emailNorm = req.Email?.Trim().ToLowerInvariant();

            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = OpName,
                ["email"] = emailNorm,
                ["token_masked"] = Mask(raw)
            });

            if (string.IsNullOrWhiteSpace(raw))
            {
                return RedirectInvalid(MsgTokenRequired, appCfg.Value, req);
            }

            if (raw.Length > MaxTokenLength)
            {
                return RedirectInvalid(MsgInvalidOrExpired, appCfg.Value, req);
            }

            var token = raw.StartsWith(ResetPrefix, StringComparison.OrdinalIgnoreCase) ? raw : $"{ResetPrefix}{raw}";
            var core = token.StartsWith(ResetPrefix, StringComparison.OrdinalIgnoreCase) ? token[ResetPrefix.Length..] : token;

            if (!Base64Url.IsMatch(core))
            {
                return RedirectInvalid(MsgInvalidOrExpired, appCfg.Value, req);
            }

            var now = DateTimeOffset.UtcNow;

            var rt = await db.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Token == token && !t.Revoked && t.ExpiresAt > now, ct);

            if (rt is null)
            {
                return RedirectInvalid(MsgInvalidOrExpired, appCfg.Value, req);
            }

            var account = await db.UserAccounts
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.EmployeeId == rt.EmployeeId, ct);

            if (account is null)
            {
                return RedirectInvalid(MsgInvalidOrExpired, appCfg.Value, req);
            }

            if (!string.IsNullOrWhiteSpace(emailNorm) &&
                !string.Equals(account.Email, emailNorm, StringComparison.Ordinal))
            {
                return RedirectInvalid(MsgInvalidOrExpired, appCfg.Value, req);
            }

            // build the front-end link where user can set a new password
            var uiUrl = BuildUiUrl(appCfg.Value, req.ClientRedirectPath ?? DefaultValidPath,
                new Dictionary<string, string?>
                {
                    ["token"] = core,
                    ["email"] = emailNorm ?? account.Email
                });

            return new Result(HttpStatusCode.OK)
            {
                Valid = true,
                RedirectUrl = uiUrl
            };
        }

        private static Result RedirectInvalid(string reason, AppSettings app, Query req)
        {
            var url = BuildUiUrl(app, req.ClientRedirectPath ?? DefaultInvalidPath,
                new Dictionary<string, string?> { ["reason"] = "invalid_or_expired" });

            return new Result(HttpStatusCode.BadRequest)
            {
                ErrorMessage = reason,
                Valid = false,
                RedirectUrl = url
            };
        }

        private static string BuildUiUrl(AppSettings app, string path, IDictionary<string, string?> query)
        {
            var baseUrl = (app.UIBaseUrl ?? app.BaseUrl ?? "/").TrimEnd('/');
            var p = path.StartsWith("/") ? path : "/" + path;

            var q = string.Join("&",
                query
                    .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

            return string.IsNullOrEmpty(q) ? $"{baseUrl}{p}" : $"{baseUrl}{p}?{q}";
        }

        private static string Mask(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return string.Empty;
            }

            const int keep = 3;
            if (token.Length <= keep * 2)
            {
                return token;
            }

            return $"{token[..keep]}...{token[^keep..]}";
        }
    }
}
