using System.ComponentModel.DataAnnotations;
using System.Net;

using CTCare.Infrastructure.Persistence;
using CTCare.Shared.BasicResult;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CTCare.Infrastructure.Command;

public static class Logout
{
    public sealed class Command: IRequest<Result>
    {
        [Required]
        public string? RefreshToken { get; set; }

        public bool RevokeAllSessions { get; set; }

        public Guid? EmployeeId { get; set; }
    }

    public sealed class Result: BasicActionResult
    {
        public Result(HttpStatusCode status) : base(status) { }
        public Result(string error) : base(error) { }
        public int RevokedCount { get; init; }
    }

    public sealed class Handler(CtCareDbContext db, ILogger<Handler> log)
        : IRequestHandler<Command, Result>
    {
        private const string MsgBadRequest = "Either RefreshToken must be provided or RevokeAllSessions must be true.";
        private const string MsgUnauthorized = "Invalid refresh token.";

        public async Task<Result> Handle(Command req, CancellationToken ct)
        {
            using var _ = log.BeginScope(new Dictionary<string, object?>
            {
                ["op"] = "logout",
                ["revokeAll"] = req.RevokeAllSessions
            });

            var now = DateTimeOffset.UtcNow;

            var employeeId = req.EmployeeId;

            if (!employeeId.HasValue && !string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                var rtRow = await db.RefreshTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Token == req.RefreshToken, ct);

                if (rtRow == null)
                {
                    return Unauthorized();
                }

                employeeId = rtRow.EmployeeId;
            }

            if (!employeeId.HasValue && !req.RevokeAllSessions && string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                return new Result(HttpStatusCode.BadRequest) { ErrorMessage = MsgBadRequest };
            }

            var revoked = 0;

            // RevokeAsync one token
            if (!string.IsNullOrWhiteSpace(req.RefreshToken))
            {
                var row = await db.RefreshTokens
                    .Where(r => r.Token == req.RefreshToken && !r.Revoked && r.ExpiresAt > now)
                    .ExecuteUpdateAsync(up => up.SetProperty(r => r.Revoked, true), ct);

                revoked += row;
            }

            // RevokeAsync all sessions for user
            if (req.RevokeAllSessions && employeeId.HasValue)
            {
                var rows = await db.RefreshTokens
                    .Where(r => r.EmployeeId == employeeId.Value && !r.Revoked && r.ExpiresAt > now)
                    .ExecuteUpdateAsync(up => up.SetProperty(r => r.Revoked, true), ct);

                revoked += rows;
            }

            return new Result(HttpStatusCode.NoContent) { RevokedCount = revoked };

            Result Unauthorized(string? msg = null)
                => new(HttpStatusCode.Unauthorized) { ErrorMessage = msg ?? MsgUnauthorized };
        }
    }
}
