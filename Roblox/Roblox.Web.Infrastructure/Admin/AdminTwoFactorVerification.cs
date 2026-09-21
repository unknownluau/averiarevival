namespace Roblox.Web.Infrastructure.Admin;

public static class AdminTwoFactorVerification
{
    private const string RedisKeyPrefix = "admin:2fa:v2:";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(20);

    public static string GetKey(long userId, string sessionId)
    {
        return RedisKeyPrefix + userId + ":" + sessionId;
    }

    public static async Task<bool> IsVerifiedAsync(long userId, string sessionId)
    {
        var value = await Roblox.Services.Cache.distributed.StringGetAsync(GetKey(userId, sessionId));
        return value != null;
    }

    public static Task MarkVerifiedAsync(long userId, string sessionId)
    {
        return Roblox.Services.Cache.distributed.StringSetAsync(GetKey(userId, sessionId), "1", Ttl);
    }

    public static Task InvalidateAsync(long userId, string sessionId)
    {
        return Roblox.Services.Cache.distributed.KeyDeleteAsync(GetKey(userId, sessionId));
    }
}
