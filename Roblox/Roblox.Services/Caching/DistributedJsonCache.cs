using System.Collections.Concurrent;
using System.Text.Json;

namespace Roblox.Services.Caching;

public sealed class DistributedJsonCache : ServiceBase, IService
{
    private static readonly ConcurrentDictionary<string, RefCountedKeyLock> KeyLocks = new();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed class RefCountedKeyLock
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References;
    }

    private sealed class CacheEnvelope<T>
    {
        public T? Value { get; set; }
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T?>> factory)
    {
        var cached = await TryGetAsync<T>(key);
        if (cached.Exists)
        {
            return cached.Value;
        }

        var keyLock = KeyLocks.GetOrAdd(key, _ => new RefCountedKeyLock());
        Interlocked.Increment(ref keyLock.References);
        await keyLock.Semaphore.WaitAsync();
        try
        {
            cached = await TryGetAsync<T>(key);
            if (cached.Exists)
            {
                return cached.Value;
            }

            var created = await factory();
            if (created is null)
            {
                return default;
            }

            await SetAsync(key, created, ttl);
            return created;
        }
        finally
        {
            keyLock.Semaphore.Release();
            if (Interlocked.Decrement(ref keyLock.References) == 0)
            {
                ((ICollection<KeyValuePair<string, RefCountedKeyLock>>)KeyLocks)
                    .Remove(new KeyValuePair<string, RefCountedKeyLock>(key, keyLock));
            }
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        var payload = JsonSerializer.Serialize(new CacheEnvelope<T> { Value = value }, SerializerOptions);
        await redis.StringSetAsync(key, payload, ttl);
    }

    public async Task RemoveAsync(string key)
    {
        await redis.KeyDeleteAsync(key);
    }

    public async Task<CacheLookupResult<T>> TryGetAsync<T>(string key)
    {
        var raw = await redis.StringGetAsync(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return CacheLookupResult<T>.Miss();
        }

        var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(raw, SerializerOptions);
        if (envelope is null)
        {
            return CacheLookupResult<T>.Miss();
        }

        return CacheLookupResult<T>.Hit(envelope.Value);
    }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return true;
    }
}

public readonly record struct CacheLookupResult<T>(bool Exists, T? Value)
{
    public static CacheLookupResult<T> Miss() => new(false, default);
    public static CacheLookupResult<T> Hit(T? value) => new(true, value);
}
