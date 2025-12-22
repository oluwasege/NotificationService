using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using System.Collections.Concurrent;

namespace NotificationService.Infrastructure.Caching;

/// <summary>
/// In-memory implementation of ICacheService using IMemoryCache.
/// </summary>
public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public MemoryCacheService(
        IMemoryCache cache,
        ILogger<MemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        if (_cache.TryGetValue<T>(key, out var cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        var value = await factory();

        if (value != null)
        {
            await SetAsync(key, value, absoluteExpiration, slidingExpiration);
        }

        return value;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue<T>(key, out var value))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return Task.FromResult<T?>(value);
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        var options = new MemoryCacheEntryOptions();

        if (absoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration;
        }
        else if (slidingExpiration.HasValue)
        {
            options.SlidingExpiration = slidingExpiration;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = DefaultExpiration;
        }

        options.RegisterPostEvictionCallback((evictedKey, evictedValue, reason, state) =>
        {
            _keys.TryRemove(evictedKey.ToString()!, out _);
        });

        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);

        _logger.LogDebug("Cached key: {Key}", key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);

        _logger.LogDebug("Removed cache key: {Key}", key);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        var keysToRemove = _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        _logger.LogDebug("Removed {Count} cache keys with prefix: {Prefix}", keysToRemove.Count, prefix);

        return Task.CompletedTask;
    }
}
