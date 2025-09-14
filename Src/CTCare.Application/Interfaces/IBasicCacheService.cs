namespace CTCare.Application.Interfaces;
/// <summary>
/// Basic key/value cache operations using a provider-agnostic abstraction.
/// </summary>
public interface IBasicCacheService
{
    Task<string?> GetAsync(string key, CancellationToken token = default);
    Task SetAsync(string key, string value, TimeSpan absoluteExpiry, TimeSpan? slidingExpiry = null, CancellationToken token = default);
    Task RemoveAsync(string key, CancellationToken token = default);
}
