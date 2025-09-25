using System.Threading.Tasks;

namespace CTCare.Application.Interfaces;
/// <summary>
/// Basic key/value cache operations using a provider-agnostic abstraction.
/// </summary>
public interface IBasicCacheService
{
    Task<string?> GetAsync(string key, CancellationToken token = default);
    Task SetAsync(string key, string value, TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, CancellationToken token = default);
    Task RemoveAsync(string key, CancellationToken token = default);

    Task SetAsync(
        string key,
        string value,
        TimeSpan? absoluteExpiry,
        IEnumerable<string>? tags,
        TimeSpan? slidingExpiry = null,
        CancellationToken cancellationToken = default);
}
