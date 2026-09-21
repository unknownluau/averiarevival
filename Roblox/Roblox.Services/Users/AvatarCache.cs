using System.Text.Json;
using Roblox.Dto.Avatar;
using Roblox.Dto.AvatarCache;

namespace Roblox.Services;

public class AvatarCache : ServiceBase, IService
{
    private Mutex updatingMutex { get; } = new();
    private Dictionary<long, DateTime> updatedInfo { get; set; } = new();

    public string GetPendingAssetsKey(long userId)
    {
        return "AvatarCache:v1:PendingAssets:" + userId;
    }
    
    public async Task<IEnumerable<long>?> GetPendingAssets(long userId)
    {
        var key = GetPendingAssetsKey(userId);
        var result = await Cache.distributed.StringGetAsync(key);
        if (result == null) return null;
        return JsonSerializer.Deserialize<AvatarCacheAsset>(result)?.assetIds;
    }

    public async Task<IEnumerable<long>?> GetAndClearPendingAssets(long userId)
    {
        var key = GetPendingAssetsKey(userId);
        var result = await Cache.distributed.StringGetDeleteAsync(key);
        if (result == null) return null;
        return JsonSerializer.Deserialize<AvatarCacheAsset>(result)?.assetIds;
    }

    public async Task SetPendingAssets(long userId, IEnumerable<long> assetIds)
    {
        await Cache.distributed.StringSetAsync(GetPendingAssetsKey(userId),
            JsonSerializer.Serialize(new AvatarCacheAsset(assetIds.Distinct())), TimeSpan.FromMinutes(1));
    }

    public async Task ClearPendingAssets(long userId)
    {
        await Cache.distributed.KeyDeleteAsync(GetPendingAssetsKey(userId));
    }
    
    public string GetPendingColorsKey(long userId)
    {
        return "AvatarCache:v1:PendingColors:" + userId;
    }

    public async Task<ColorEntry?> GetColors(long userId)
    {
        var key = GetPendingColorsKey(userId);
        var result = await Cache.distributed.StringGetAsync(key);
        if (result == null) return null;
        return JsonSerializer.Deserialize<ColorEntry>(result);
    }

    public async Task<ColorEntry?> GetAndClearColors(long userId)
    {
        var key = GetPendingColorsKey(userId);
        var result = await Cache.distributed.StringGetDeleteAsync(key);
        if (result == null) return null;
        return JsonSerializer.Deserialize<ColorEntry>(result);
    }
    
    public async Task SetColors(long userId, ColorEntry colors)
    {
        await Cache.distributed.StringSetAsync(GetPendingColorsKey(userId),
            JsonSerializer.Serialize(colors), TimeSpan.FromMinutes(1));
    }
    public async Task DeletePendingAssets(long userId)
    {
        await Cache.distributed.KeyDeleteAsync(GetPendingAssetsKey(userId));
    }

    public async Task DeletePendingColors(long userId)
    {
        await Cache.distributed.KeyDeleteAsync(GetPendingColorsKey(userId));
    }

    public async Task DeleteAvatarCache(long userId)
    {
        await DeletePendingAssets(userId);
        await DeletePendingColors(userId);
    }

    public async Task<bool> HasPendingAvatarChanges(long userId)
    {
        var pendingAssets = await GetPendingAssets(userId);
        if (pendingAssets != null) return true;

        var pendingColors = await GetColors(userId);
        return pendingColors != null;
    }

    public bool AttemptScheduleRender(long userId)
    {
        lock (updatingMutex)
        {
            if (updatedInfo.ContainsKey(userId))
            {
                return false;
            }
            updatedInfo[userId] = DateTime.UtcNow;
            return true;
        }
    }

    public void UnscheduleRender(long userId)
    {
        lock (updatingMutex)
        {
            if (updatedInfo.ContainsKey(userId))
                updatedInfo.Remove(userId);
        }
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
