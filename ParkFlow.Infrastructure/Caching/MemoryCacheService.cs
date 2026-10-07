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
    private readonly ConcurrentDictionary<string, Guid> _activeKeys = new();

    private readonly object _gate = new();
    private long _generation;
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _pending = new();

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

            var version = Guid.NewGuid();
            options.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
            {
                if (evictedKey is string stringKey)
                {
                    lock (_gate)
                    {
                        if (_activeKeys.TryGetValue(stringKey, out var current) && current == version)
                            _activeKeys.TryRemove(stringKey, out _);
                    }
                }
            });

            lock (_gate)
            {
                _memoryCache.Set(key, value, options);
                _activeKeys[key] = version;
            }
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
            lock (_gate)
            {
                _generation++;
                _pending.Clear();
                _memoryCache.Remove(key);
                _activeKeys.TryRemove(key, out _);
            }
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
            lock (_gate)
            {
                _generation++;
                _pending.Clear();
                var matchingKeys = _activeKeys.Keys
                    .Where(k => k.StartsWith(prefixKey, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var key in matchingKeys)
                {
                    _memoryCache.Remove(key);
                    _activeKeys.TryRemove(key, out _);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove cache items by prefix '{Prefix}'", prefixKey);
        }

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key, Func<Task<T>> factory, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(key)) return await factory();

        Lazy<Task<object?>> work;
        lock (_gate)
        {
            if (_memoryCache.TryGetValue(key, out var cached) && cached is T typed) return typed;
            var started = _generation;
            work = _pending.GetOrAdd(key, _ => new Lazy<Task<object?>>(async () =>
            {
                var result = await factory();
                lock (_gate)
                {
                    // A write invalidated this read while its database query was running.
                    if (started == _generation && result is not null)
                        SetAsync(key, result, expiration).GetAwaiter().GetResult();
                }
                return result;
            }));
        }
        // Each waiter may cancel independently; cleanup belongs to the shared work.
        var task = work.Value;
        _ = task.ContinueWith(completed =>
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, work))
                    _pending.TryRemove(key, out _);
            }
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return (T)(await task.WaitAsync(cancellationToken))!;
    }
}
