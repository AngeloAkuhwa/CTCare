using CTCare.Application.Interfaces;
using CTCare.Shared.Settings;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace CTCare.Application.Services;
/// <summary>
/// Redis-based cache implementation with TTL and tagging support.
/// </summary>
public class CacheService: ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IDatabase _db;
    private readonly ILogger<CacheService> _logger;
    private readonly RedisSetting _settings;

    public CacheService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        IOptions<RedisSetting> redisOptions,
        ILogger<CacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        if (redis == null)
        {
            throw new ArgumentNullException(nameof(redis));
        }

        _db = redis.GetDatabase();
        _settings = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(key));
        }

        try
        {
            return await _cache.GetStringAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}.", key);
            return null;
        }
    }

    public async Task SetAsync(
        string key,
        string value,
        TimeSpan absoluteExpiry,
        TimeSpan? slidingExpiry = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiry,
            SlidingExpiration = slidingExpiry ?? _settings.SlidingExpiration
        };

        try
        {
            await _cache.SetStringAsync(key, value, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}.", key);
        }
    }

    public async Task AddTagAsync(string tag, string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var tagSet = ApplyTagKey(tag);
        try
        {
            await _db.SetAddAsync(tagSet, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding key {Key} to tag {Tag}.", key, tag);
        }
    }

    public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        var tagSet = ApplyTagKey(tag);
        try
        {
            var members = await _db.SetMembersAsync(tagSet).ConfigureAwait(false);
            foreach (var member in members)
            {
                await RemoveAsync(member, cancellationToken);
            }
            await _db.KeyDeleteAsync(tagSet).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache by tag {Tag}.", tag);
        }
    }

    public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(nameof(key));
        }

        try
        {
            var ttl = await _db.KeyTimeToLiveAsync(key).ConfigureAwait(false);
            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve TTL for cache key {Key}.", key);
            return null;
        }
    }

    private static string ApplyTagKey(string tag) => $"tag:{tag}";
}
