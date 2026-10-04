using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ParkFlow.Application.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Infrastructure.Caching;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService>? _logger;
    private readonly ConcurrentDictionary<string, byte> _activeKeys = new();

    public MemoryCacheService(
        IMemoryCache memoryCache,
        ILogger<MemoryCacheService>? logger = null)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.FromResult<T?>(default);

        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue) && cachedValue is T typedValue)
            {
                return Task.FromResult<T?>(typedValue);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to retrieve cache item with key '{Key}'", key);
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value == null)
            return Task.CompletedTask;

        try
        {
            var effectiveExpiration = expiration ?? TimeSpan.FromMinutes(5);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = effectiveExpiration
            };

            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                if (evictedKey is string stringKey)
                {
                    _activeKeys.TryRemove(stringKey, out _);
                }
            });

            _memoryCache.Set(key, value, options);
            _activeKeys.TryAdd(key, 0);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set cache item with key '{Key}'", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Task.CompletedTask;

        try
        {
            _memoryCache.Remove(key);
            _activeKeys.TryRemove(key, out _);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove cache item with key '{Key}'", key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefixKey))
            return Task.CompletedTask;

        try
        {
            var matchingKeys = _activeKeys.Keys
                .Where(k => k.StartsWith(prefixKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in matchingKeys)
            {
                _memoryCache.Remove(key);
                _activeKeys.TryRemove(key, out _);
            }

            if (matchingKeys.Count > 0)
            {
                _logger?.LogDebug("Evicted {Count} cache entries with prefix '{Prefix}'", matchingKeys.Count, prefixKey);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove cache items by prefix '{Prefix}'", prefixKey);
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return await factory();

        try
        {
            if (_memoryCache.TryGetValue(key, out var cachedValue) && cachedValue is T typedValue)
            {
                return typedValue;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error reading cache key '{Key}' in GetOrCreateAsync", key);
        }

        var result = await factory();

        if (result != null)
        {
            await SetAsync(key, result, expiration, cancellationToken);
        }

        return result;
    }
}
