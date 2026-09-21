using Roblox.Dto.Cooldown;
using StackExchange.Redis;

namespace Roblox.Services;

public class CooldownException : System.Exception
{
    
}

public class CooldownService : ServiceBase, IService
{
    private const string BucketKeyPrefix = "BucketCooldown:v2:";
    private const string IncrementBucketLua = @"
local now = tonumber(ARGV[1])
local cutoff = tonumber(ARGV[2])
local limit = tonumber(ARGV[3])
local periodSeconds = tonumber(ARGV[4])
local incrementOnFailure = ARGV[5] == '1'
redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', cutoff)
local current = redis.call('ZCARD', KEYS[1])
local allowed = current < limit
if allowed or incrementOnFailure then
    redis.call('ZADD', KEYS[1], now, ARGV[6])
    redis.call('EXPIRE', KEYS[1], periodSeconds)
end
if allowed then return 1 else return 0 end";

    private const string GetBucketScoresLua = @"
local cutoff = tonumber(ARGV[1])
local periodSeconds = tonumber(ARGV[2])
redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', cutoff)
redis.call('EXPIRE', KEYS[1], periodSeconds)
local values = redis.call('ZRANGE', KEYS[1], 0, -1, 'WITHSCORES')
local scores = {}
for i = 2, #values, 2 do
    scores[#scores + 1] = values[i]
end
return scores";

    private static string BucketKey(string key) => BucketKeyPrefix + key;

    public async Task<bool> TryCooldownCheck(string key, System.TimeSpan minimumRequestSpacing)
    {
        return await redis.StringSetIfNotExistsAsync(key, "{}", minimumRequestSpacing);
    }
    
    [Obsolete("Use TryCooldownCheck instead")]
    public async Task CooldownCheck(string key, System.TimeSpan minimumRequestSpacing)
    {
        if (!await TryCooldownCheck(key, minimumRequestSpacing))
            throw new CooldownException();
    }

    public async Task ResetCooldown(string key)
    {
        await redis.KeyDeleteAsync(key);
    }

    public async Task<IEnumerable<RateLimitBucketEntry>> GetBucketDataForKey(string key, TimeSpan period)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (long)period.TotalMilliseconds;
        var result = await redis.ScriptEvaluateAsync(
            GetBucketScoresLua,
            new RedisKey[] { BucketKey(key) },
            new RedisValue[] { cutoff, Math.Max(1, (long)period.TotalSeconds) });

        var scores = (RedisResult[]?)result ?? Array.Empty<RedisResult>();
        return scores
            .Select(score => long.TryParse(score.ToString(), out var createdAt) ? createdAt : 0)
            .Where(createdAt => createdAt > 0)
            .Select(createdAt => DateTimeOffset.FromUnixTimeMilliseconds(createdAt).UtcDateTime)
            .Select(createdAt => new RateLimitBucketEntry(createdAt));
    }
    
    public async Task<bool> TryIncrementBucketCooldown(string key, long requestsPerPeriod, TimeSpan period, IEnumerable<RateLimitBucketEntry> entries, bool incrementOnFailure = false)
    {
        return await TryIncrementBucketCooldown(key, requestsPerPeriod, period, incrementOnFailure);
    }

    public async Task<bool> TryIncrementBucketCooldown(string key, long requestsPerPeriod, TimeSpan period, bool incrementOnFailure = false)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cutoff = now - (long)period.TotalMilliseconds;
        var result = await redis.ScriptEvaluateAsync(
            IncrementBucketLua,
            new RedisKey[] { BucketKey(key) },
            new RedisValue[]
            {
                now,
                cutoff,
                requestsPerPeriod,
                Math.Max(1, (long)period.TotalSeconds),
                incrementOnFailure ? "1" : "0",
                now + ":" + Guid.NewGuid().ToString("N"),
            });
        return (long)result == 1;
    }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}
