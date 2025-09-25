using System.Text.RegularExpressions;

using CTCare.Application.Interfaces;
using CTCare.Shared.Settings;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace CTCare.Application.Services;

/// <summary>
/// Redis-based cache with TTL and tagging, aligned with IDistributedCache instance prefix,
/// robust key normalization, and tag lifecycle helpers.
/// </summary>
public class CacheService(
    IDistributedCache cache,
    IConnectionMultiplexer redis,
    IOptions<RedisSetting> redisOptions,
    IOptions<RedisCacheOptions> redisCacheOptions,
    ILogger<CacheService> logger)
    : ICacheService
{
    private readonly IDistributedCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly IDatabase _db = (redis ?? throw new ArgumentNullException(nameof(redis))).GetDatabase();
    private readonly ILogger<CacheService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    // This must match how AddStackExchangeRedisCache was configured (InstanceName).
    private readonly string _instancePrefix = (redisCacheOptions?.Value?.InstanceName ?? string.Empty);

    private static readonly Regex BadKeyChars = new(@"[\s\r\n\t]+", RegexOptions.Compiled);


    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);

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
        TimeSpan? absoluteExpiry = null,
        TimeSpan? slidingExpiry = null,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiry ?? redisOptions.Value.AbsoluteExpiration,
            SlidingExpiration = slidingExpiry ?? redisOptions.Value.SlidingExpiration
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

    /// <summary>
    /// Atomically set a value and register one or more tags.
    /// The tag set TTL is extended up to the value's absolute TTL to avoid orphan tag sets.
    /// </summary>
    public async Task SetAsync(
        string key,
        string value,
        TimeSpan? absoluteExpiry,
        IEnumerable<string>? tags,
        TimeSpan? slidingExpiry = null,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        ArgumentNullException.ThrowIfNull(value);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiry ?? redisOptions.Value.AbsoluteExpiration,
            SlidingExpiration = slidingExpiry ?? redisOptions.Value.SlidingExpiration
        };

        try
        {
            await _cache.SetStringAsync(key, value, options, cancellationToken);

            if (tags is not null)
            {
                var tasks = new List<Task>();
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    var setKey = TagSetKey(tag);
                    tasks.Add(_db.SetAddAsync(setKey, key));

                    // Extend tag-set TTL to at least value TTL (prevents endless growth of tag sets)
                    tasks.Add(ExtendKeyTtlIfShorterAsync(setKey, absoluteExpiry ?? redisOptions.Value.AbsoluteExpiration));
                }
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key} with tags.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        key = NormalizeKey(key);

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

        key = NormalizeKey(key);
        var setKey = TagSetKey(tag);

        try
        {
            await _db.SetAddAsync(setKey, key);
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

        var setKey = TagSetKey(tag);

        try
        {
            var members = await _db.SetMembersAsync(setKey).ConfigureAwait(false);
            if (members.Length == 0)
            {
                await _db.KeyDeleteAsync(setKey).ConfigureAwait(false);
                return;
            }

            // Remove through IDistributedCache so provider prefix is respected
            var removals = members
                .Select(m => m.HasValue ? m.ToString() : null)
                .Where(static s => !string.IsNullOrWhiteSpace(s))!
                .Select(s => _cache.RemoveAsync(s!, cancellationToken));

            await Task.WhenAll(removals);

            // Finally, drop the tag set itself
            await _db.KeyDeleteAsync(setKey).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating cache by tag {Tag}.", tag);
        }
    }

    public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        try
        {
            // IMPORTANT: query TTL for the PHYSICAL key (incl. provider InstanceName)
            var physicalKey = PhysicalKey(key);
            var ttl = await _db.KeyTimeToLiveAsync(physicalKey).ConfigureAwait(false);
            return ttl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve TTL for cache key {Key}.", key);
            return null;
        }
    }


    private string TagSetKey(string tag)
    {
        var normalized = NormalizeKey(tag);
        return $"{_instancePrefix}tag:{normalized}";
    }

    private string PhysicalKey(string logicalKey)
    {
        // StackExchangeRedisCache prepends InstanceName to logical keys
        // so TTL queries must include that prefix to hit the same Redis key.
        return $"{_instancePrefix}{logicalKey}";
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cache key cannot be empty.", nameof(key));
        }

        var k = key.Trim();
        k = BadKeyChars.Replace(k, " ");
        return k.ToLowerInvariant();
    }

    private async Task ExtendKeyTtlIfShorterAsync(RedisKey key, TimeSpan desiredTtl)
    {
        // If no TTL, set; if TTL shorter, extend; else leave as-is
        var current = await _db.KeyTimeToLiveAsync(key).ConfigureAwait(false);
        if (current is null || current < desiredTtl)
        {
            await _db.KeyExpireAsync(key, desiredTtl).ConfigureAwait(false);
        }
    }
}
