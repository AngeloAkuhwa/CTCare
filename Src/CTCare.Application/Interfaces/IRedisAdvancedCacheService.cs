namespace CTCare.Application.Interfaces;
/// <summary>
/// Advanced Redis-specific operations: TTL introspection and tag-based invalidation.
/// </summary>
public interface IRedisAdvancedCacheService
{
    Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken token = default);
    Task AddTagAsync(string tag, string key, CancellationToken token = default);
    Task InvalidateByTagAsync(string tag, CancellationToken token = default);
}
