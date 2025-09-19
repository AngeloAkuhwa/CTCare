using CTCare.Application.Interfaces;
using CTCare.Shared.Models;
using CTCare.Shared.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CTCare.Application.Services;

public sealed class LoginAttemptService(
    ICacheService cache,
    IOptions<AuthSettings> options,
    ILogger<LoginAttemptService> logger
): ILoginAttemptService
{
    private readonly AuthSettings _opt = options.Value;

    public async Task<LoginAttemptResult> CheckStatusAsync(string failKey, string lockKey, CancellationToken ct = default)
    {
        var lockVal = await cache.GetAsync(lockKey, ct);

        if (!string.IsNullOrEmpty(lockVal))
        {
            var ttl = await cache.GetTimeToLiveAsync(lockKey, ct);
            logger.LogDebug("Account locked: {LockKey}, TTL: {TTL}", lockKey, ttl);
            return new LoginAttemptResult { IsLocked = true, RemainingLockout = ttl, FailedCount = 0 };
        }

        var failCountStr = await cache.GetAsync(failKey, ct);
        var count = int.TryParse(failCountStr, out var c) ? c : 0;
        return new LoginAttemptResult { IsLocked = false, FailedCount = count, RemainingLockout = null };
    }

    public async Task<LoginAttemptResult> RegisterFailureAsync(string failKey, string lockKey, CancellationToken ct = default)
    {
        var status = await CheckStatusAsync(failKey, lockKey, ct);
        if (status.IsLocked)
        {
            return status;
        }

        var newCount = status.FailedCount + 1;

        if (newCount >= _opt.MaxFailedAttempts)
        {
            await cache.RemoveAsync(failKey, ct);
            var duration = _opt.LockoutDuration; // optionally multiply by newCount for progressive
            await cache.SetAsync(lockKey, "LOCKED", duration, null, ct);
            logger.LogWarning("Locking out {LockKey} for {Duration} after {Failures} failures.", lockKey, duration, newCount);

            return new LoginAttemptResult { IsLocked = true, FailedCount = 0, RemainingLockout = duration };
        }

        await cache.SetAsync(failKey, newCount.ToString(), _opt.RetryWindow, null, ct);
        logger.LogInformation("Recorded failure #{Count} for {FailKey}.", newCount, failKey);

        return new LoginAttemptResult { IsLocked = false, FailedCount = newCount, RemainingLockout = null };
    }

    public async Task ResetAsync(CancellationToken ct = default, params string[]? keys)
    {
        if (keys is null || keys.Length == 0)
        {
            return;
        }

        await Task.WhenAll(keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => cache.RemoveAsync(k!, ct)));
        logger.LogDebug("Reset cache keys: {Keys}.", string.Join(", ", keys));
    }
}
