using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace VendingAdSystem.Application.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default);
}

public class NullCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public MemoryCacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _memoryCache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out _))
            return Task.FromResult(false);

        _memoryCache.Set(key, token, ttl);
        return Task.FromResult(true);
    }

    public Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }
}

public class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var raw = await db.StringGetAsync(key).ConfigureAwait(false);
        if (raw.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>(raw!, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var payload = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(key, payload, ttl).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        await db.KeyDeleteAsync(key).ConfigureAwait(false);
    }

    public async Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        // SET NX with an expiry is the Redis pattern for a short-lived distributed lock.
        return await db.StringSetAsync(key, token, ttl, When.NotExists).ConfigureAwait(false);
    }

    public async Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        // Release only if the stored token matches, so one request cannot delete
        // another request's lock after the original lock expired and was reacquired.
        var script = "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
        await db.ScriptEvaluateAsync(script, new RedisKey[] { key }, new RedisValue[] { token }).ConfigureAwait(false);
    }
}
