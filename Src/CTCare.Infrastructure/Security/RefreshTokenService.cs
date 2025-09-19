using System.Security.Cryptography;

using CTCare.Domain.Entities;
using CTCare.Infrastructure.Persistence;
using CTCare.Shared.Utilities;

using Microsoft.EntityFrameworkCore;

namespace CTCare.Infrastructure.Security;

public interface IRefreshTokenService
{
    RefreshToken Issue(Guid employeeId, int validityDays, CtCareDbContext context, CancellationToken ct);
    Task RevokeAsync(string token, CtCareDbContext context, CancellationToken ct);
    Task RevokeAllActiveAsync(Guid employeeId, CtCareDbContext context, CancellationToken ct);
    Task TrimActiveAsync(Guid employeeId, int keep, CtCareDbContext context, CancellationToken ct);
    string GenerateUrlSafeToken(int bytes = 32);
}


public sealed class RefreshTokenService(): IRefreshTokenService
{
    public RefreshToken Issue(Guid employeeId, int validityDays, CtCareDbContext context, CancellationToken ct)
    {
        var token = new RefreshToken
        {
            Id = SequentialGuid.NewGuid(),
            EmployeeId = employeeId,
            Token = GenerateUrlSafeToken(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, validityDays)),
            Revoked = false,
            UpdatedBy = employeeId
        };

        context.RefreshTokens.Add(token);
        return token;
    }

    public async Task RevokeAsync(string token, CtCareDbContext context, CancellationToken ct)
    {
        var rt = await context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (rt is null)
        {
            return;
        }

        rt.Revoked = true;
    }

    public async Task RevokeAllActiveAsync(Guid employeeId, CtCareDbContext context, CancellationToken ct)
    {
        var actives = await context.RefreshTokens
            .Where(r => r.EmployeeId == employeeId && !r.Revoked && r.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var t in actives)
        {
            t.Revoked = true;
        }
    }

    public async Task TrimActiveAsync(Guid employeeId, int keep, CtCareDbContext context, CancellationToken ct)
    {
        var extra = await context.RefreshTokens
            .Where(r => r.EmployeeId == employeeId && !r.Revoked && r.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(r => r.CreatedAt)
            .Skip(keep)
            .ToListAsync(ct);

        foreach (var r in extra)
        {
            r.Revoked = true;
        }
    }

    public string GenerateUrlSafeToken(int bytes = 32)
    {
        Span<byte> buffer = stackalloc byte[Math.Max(16, bytes)];
        RandomNumberGenerator.Fill(buffer);
        // URL-safe base64 without padding
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
