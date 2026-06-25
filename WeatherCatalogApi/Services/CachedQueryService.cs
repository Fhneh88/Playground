using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace WeatherCatalogApi.Services;

public sealed record CachedQueryServiceOptions
{
    // Must match the InstanceName passed to AddStackExchangeRedisCache -
    // used to strip the prefix when removing keys from MemoryCache during SCAN.
    public string RedisInstanceName { get; init; } = string.Empty;
}

public sealed class CachedQueryService : ICachedQueryService, IDisposable
{
    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly IConnectionMultiplexer _redis;
    private readonly string _redisPrefix;

    // One SemaphoreSlim per unique key. Cache key spaces are bounded in practice,
    // so we accept accumulation without cleanup. Disposed in Dispose().
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
        new(StringComparer.Ordinal);

    public CachedQueryService(
        IMemoryCache l1,
        IDistributedCache l2,
        IConnectionMultiplexer redis,
        CachedQueryServiceOptions? options = null)
    {
        _l1 = l1;
        _l2 = l2;
        _redis = redis;
        _redisPrefix = options?.RedisInstanceName ?? string.Empty;
    }

    public async Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        // ── L1: MemoryCache ───────────────────────────────────────────────────
        if (_l1.TryGetValue(key, out T? l1Value))
            return l1Value;

        // ── L2: Redis (no lock yet - fast path for warm cache) ────────────────
        byte[]? l2Bytes = await TryGetL2Async(key, ct);
        if (l2Bytes is not null)
        {
            var l2Value = Deserialize<T>(l2Bytes);
            _l1.Set(key, l2Value, ttl);
            return l2Value;
        }

        // ── Acquire per-key semaphore ─────────────────────────────────────────
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        try
        {
            // Double-check L1
            if (_l1.TryGetValue(key, out l1Value))
                return l1Value;

            // Double-check L2
            l2Bytes = await TryGetL2Async(key, ct);
            if (l2Bytes is not null)
            {
                var l2Value = Deserialize<T>(l2Bytes);
                _l1.Set(key, l2Value, ttl);
                return l2Value;
            }

            // Cache miss - call factory and populate both levels
            var result = await factory(ct);
            if (result is not null)
            {
                await TrySetL2Async(key, result, ttl, ct);
                _l1.Set(key, result, ttl);
            }
            return result;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task InvalidateAsync(string key, CancellationToken ct = default)
    {
        _l1.Remove(key);
        try { await _l2.RemoveAsync(key, ct); }
        catch { /* Redis unreachable; L1 cleared, L2 will expire naturally */ }
    }

    public async Task InvalidateByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        // IDistributedCache has no SCAN; go through the multiplexer directly.
        var db = _redis.GetDatabase();
        var pattern = $"{_redisPrefix}{prefix}*";

        foreach (var server in _redis.GetServers().Where(s => s.IsConnected))
        {
            await foreach (var redisKey in server.KeysAsync(pattern: pattern))
            {
                ct.ThrowIfCancellationRequested();

                string fullKey = (string)redisKey!;

                // Strip the instance prefix to reconstruct the logical MemoryCache key.
                string logicalKey = _redisPrefix.Length > 0
                    && fullKey.StartsWith(_redisPrefix, StringComparison.Ordinal)
                        ? fullKey[_redisPrefix.Length..]
                        : fullKey;

                _l1.Remove(logicalKey);
                await db.KeyDeleteAsync(redisKey);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<byte[]?> TryGetL2Async(string key, CancellationToken ct)
    {
        try { return await _l2.GetAsync(key, ct); }
        catch { return null; } // Redis unreachable → treat as miss
    }

    private async Task TrySetL2Async<T>(string key, T value, TimeSpan ttl, CancellationToken ct)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _l2.SetAsync(key, bytes,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);
        }
        catch { /* Redis unreachable; L1 still populated */ }
    }

    private static T? Deserialize<T>(byte[] bytes)
    {
        try { return JsonSerializer.Deserialize<T>(bytes); }
        catch { return default; }
    }

    public void Dispose()
    {
        foreach (var s in _locks.Values)
            s.Dispose();
        _locks.Clear();
    }
}
