using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using Roblox.Cache;
using StackExchange.Redis;

namespace Roblox.Services;

public static class Cache
{
    public static DistributedCache distributed { get; } = new();
    private static RedLockFactory? _redLock;
    public static RedLockFactory redLock
    {
        get => _redLock ?? throw new Exception("RedLock is not available");
        private set => _redLock = value;
    }

    public static void Configure(string connectUrl, string? password = null)
    {
        Roblox.Cache.DistributedCache.Configure(connectUrl, password);
        redLock = RedLockFactory.Create(new List<RedLockMultiplexer>()
        {
            Roblox.Cache.DistributedCache.redis,
        });
    }
}