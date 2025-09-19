using System.Security.Cryptography;

using CTCare.Application.Interfaces;
using CTCare.Shared.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Infrastructure.Security;

public interface IOtpService
{
    Task<string> IssueLoginOtpAsync(string email, string ip, CancellationToken ct);
    Task<bool> VerifyLoginOtpAsync(string email, string ip, string code, CancellationToken ct);
    Task InvalidateAsync(string email, CancellationToken ct);
}

public sealed class OtpService(
    ICacheService cache,
    ILogger<OtpService> log,
    IOptions<AuthSettings> auth): IOtpService
{
    private readonly ICacheService _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger<OtpService> _log = log ?? throw new ArgumentNullException(nameof(log));

    private readonly int _digits = Math.Clamp(auth.Value.OtpLength, 4, 8);
    private readonly TimeSpan _otpTtl = auth.Value.OtpExpiry;

    public async Task<string> IssueLoginOtpAsync(string email, string ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var key = CacheKeys.Email_OtpKey(email);

        // If an OTP already exists and is still reasonably fresh, reuse it to avoid spamming inbox
        var existing = await _cache.GetAsync(key, ct);
        var ttl = await _cache.GetTimeToLiveAsync(key, ct);

        if (!string.IsNullOrWhiteSpace(existing) && ttl is { TotalSeconds: > 60 })
        {
            _log.LogInformation("Reusing existing OTP for {Email}, TTL ~{Ttl}s (IP {Ip})",
                email, (int)ttl.Value.TotalSeconds, ip ?? "-");
            return existing!;
        }

        var code = GenerateOtp(_digits);
        _log.LogInformation("Issuing new OTP for {Email} (IP {Ip})", email, ip ?? "-");

        // Store OTP with TTL; also add a tag so we can invalidate all OTPs for a user
        await _cache.SetAsync(
            key: key,
            value: code,
            absoluteExpiry: _otpTtl,
            slidingExpiry: null,
            ct
        );

        await _cache.AddTagAsync(tag: $"otp:login:{email}", key: key, ct);

        return code;
    }

    public async Task<bool> VerifyLoginOtpAsync(string email, string ip, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var key = CacheKeys.Email_OtpKey(email);
        var expected = await _cache.GetAsync(key, ct);

        var ok = !string.IsNullOrWhiteSpace(expected) &&
                 string.Equals(expected, code, StringComparison.Ordinal);

        if (ok)
        {
            _log.LogInformation("OTP verified for {Email} (IP {Ip})", email, ip ?? "-");
            await _cache.RemoveAsync(key, ct);
        }
        else
        {
            _log.LogWarning("OTP verification failed for {Email} (IP {Ip})", email, ip ?? "-");
        }

        return ok;
    }

    public async Task InvalidateAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        await _cache.InvalidateByTagAsync($"otp:login:{email}", ct);
    }

    private static string GenerateOtp(int digits)
    {
        var max = (int)Math.Pow(10, digits);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(new string('0', digits));
    }
}
