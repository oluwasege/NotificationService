using Microsoft.Extensions.Caching.Memory;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Service for caching data with configurable expiration.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from the cache, or creates it if it doesn't exist.
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null);

    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// Removes all values matching a pattern from the cache.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix);
}
