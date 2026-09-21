using Microsoft.Extensions.Caching.Memory;
using Roblox.Metrics;
using StackExchange.Redis;

namespace Roblox.Cache;

public class DistributedCache
{
    private static readonly TimeSpan DefaultLocalTtl = TimeSpan.FromSeconds(30);
    private const int LocalCacheSizeLimit = 50_000;
    private static readonly MemoryCache LocalCache = new(new MemoryCacheOptions
    {
        SizeLimit = LocalCacheSizeLimit,
    });

    private static ConnectionMultiplexer? _redis;
    public static ConnectionMultiplexer redis
    {
        get => _redis ?? throw new Exception("Redis is not available");
        private set => _redis = value;
    }

    private static IDatabase database => redis.GetDatabase(0);

    public static void Configure(string connectUrl, string? password = null)
    {
        var options = ConfigurationOptions.Parse(connectUrl);
        options.ConnectTimeout = 10000;
        options.SyncTimeout = 10000;
        options.AbortOnConnectFail = false;
        if (password != null)
        {
            options.Password = password;
        }

        redis = ConnectionMultiplexer.Connect(options);
    }

    private static string GetPrefix(string key)
    {
        var first = key.IndexOf(':');
        if (first < 0)
            return key.Length > 32 ? key[..32] : key;

        var second = key.IndexOf(':', first + 1);
        var length = second < 0 ? first : second;
        return key[..Math.Min(length, 64)];
    }

    private static TimeSpan GetLocalTtl(TimeSpan? redisTtl)
    {
        if (redisTtl is null || redisTtl.Value <= TimeSpan.Zero)
            return DefaultLocalTtl;

        return redisTtl.Value < DefaultLocalTtl ? redisTtl.Value : DefaultLocalTtl;
    }

    private static void AddToLocalCache(string key, string value, TimeSpan? redisTtl = null)
    {
        var ttl = GetLocalTtl(redisTtl);
        LocalCache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        });
    }

    private static bool TryGetLocal(string key, out string? value)
    {
        if (LocalCache.TryGetValue(key, out string? cached))
        {
            value = cached;
            PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "l1", true);
            return true;
        }

        value = null;
        PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "l1", false);
        return false;
    }

    public string? StringGetMemory(string key)
    {
        return TryGetLocal(key, out var value) ? value : null;
    }

    public async Task StringSetAsync(string key, string value)
    {
        await database.StringSetAsync(key, value);
        AddToLocalCache(key, value);
    }

    public async Task StringSetAsync(string key, string value, TimeSpan ttl)
    {
        await database.StringSetAsync(key, value, ttl);
        AddToLocalCache(key, value, ttl);
    }

    public async Task StringSetAsync(string key, long value)
    {
        await StringSetAsync(key, value.ToString());
    }

    public async Task<bool> StringSetIfNotExistsAsync(string key, string value, TimeSpan ttl)
    {
        var wasSet = await database.StringSetAsync(key, value, ttl, When.NotExists);
        if (wasSet)
            AddToLocalCache(key, value, ttl);

        return wasSet;
    }

    public async Task<string?> StringGetAsync(string key)
    {
        if (TryGetLocal(key, out var cached))
            return cached;

        var value = await database.StringGetAsync(key);
        if (value.HasValue)
        {
            var str = value.ToString();
            PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", true);
            AddToLocalCache(key, str);
            return str;
        }

        PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", false);
        return null;
    }

    public async Task<IReadOnlyDictionary<string, string?>> StringGetManyAsync(IEnumerable<string> keys)
    {
        var requested = keys.Distinct().ToArray();
        var result = new Dictionary<string, string?>(requested.Length);
        var redisKeys = new List<RedisKey>();

        foreach (var key in requested)
        {
            if (TryGetLocal(key, out var cached))
            {
                result[key] = cached;
                continue;
            }

            redisKeys.Add(key);
        }

        if (redisKeys.Count == 0)
            return result;

        var redisValues = await database.StringGetAsync(redisKeys.ToArray());
        for (var i = 0; i < redisKeys.Count; i++)
        {
            var key = (string)redisKeys[i]!;
            var value = redisValues[i];
            if (value.HasValue)
            {
                var str = value.ToString();
                result[key] = str;
                AddToLocalCache(key, str);
                PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", true);
            }
            else
            {
                result[key] = null;
                PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", false);
            }
        }

        return result;
    }

    [Obsolete("Use StringGetAsync instead. This method only remains for legacy synchronous call sites.")]
    public string? StringGet(string key)
    {
        if (TryGetLocal(key, out var cached))
            return cached;

        var value = database.StringGet(key);
        if (!value.HasValue)
        {
            PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", false);
            return null;
        }

        var str = value.ToString();
        AddToLocalCache(key, str);
        PerformanceMetrics.ReportRedisLookup(GetPrefix(key), "redis", true);
        return str;
    }

    [Obsolete("Use StringSetAsync instead. This method only remains for legacy synchronous call sites.")]
    public void StringSet(string key, string value)
    {
        database.StringSet(key, value);
        AddToLocalCache(key, value);
    }

    public async Task<long> StringIncrementAsync(string key, long value = 1)
    {
        LocalCache.Remove(key);
        return await database.StringIncrementAsync(key, value);
    }

    public async Task<bool> KeyExpireAsync(string key, TimeSpan ttl)
    {
        return await database.KeyExpireAsync(key, ttl);
    }

    public async Task KeyDeleteAsync(string key)
    {
        LocalCache.Remove(key);
        await database.KeyDeleteAsync(key);
    }

    public async Task<long> SetAddAsync(string key, string value, TimeSpan? ttl = null)
    {
        var added = await database.SetAddAsync(key, value);
        if (ttl is not null)
            await database.KeyExpireAsync(key, ttl);
        return added ? 1 : 0;
    }

    public async Task<bool> SetRemoveAsync(string key, string value)
    {
        return await database.SetRemoveAsync(key, value);
    }

    public async Task<string[]> SetMembersAsync(string key)
    {
        var members = await database.SetMembersAsync(key);
        return members.Select(v => v.ToString()).ToArray();
    }

    private const string GetDeleteLua = "local v = redis.call('GET', KEYS[1]); if v then redis.call('DEL', KEYS[1]) end; return v";

    public async Task<string?> StringGetDeleteAsync(string key)
    {
        LocalCache.Remove(key);
        var result = await database.ScriptEvaluateAsync(GetDeleteLua, [key]);
        if (result.IsNull) return null;
        return (string?)result;
    }

    public async Task<RedisResult> ScriptEvaluateAsync(string script, RedisKey[] keys, RedisValue[] values)
    {
        return await database.ScriptEvaluateAsync(script, keys, values);
    }

    public async Task PublishAsync(string channel, string message)
    {
        await database.PublishAsync(RedisChannel.Literal(channel), message);
    }
}
