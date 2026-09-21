namespace Roblox.Metrics;

public static class RenderMetrics
{
    public static void ReportAvatarThumbnailFailure(bool nullBody = false) => RobloxMetrics.RenderFailures.Add(1,
        new KeyValuePair<string, object?>("render.type", "avatar_thumbnail"),
        new KeyValuePair<string, object?>("failure.reason", nullBody ? "null_body" : "render_failed"));

    public static void ReportAvatarThumbnailDuration(long elapsedMilliseconds) => RobloxMetrics.RenderDuration.Record(elapsedMilliseconds,
        new KeyValuePair<string, object?>("render.type", "avatar_thumbnail"));

    public static void ReportAssetQueueEvent(string name, string? reason = null) =>
        RobloxMetrics.AssetRenderQueueEvents.Add(1,
            new KeyValuePair<string, object?>("queue.event", name),
            new KeyValuePair<string, object?>("failure.reason", reason));

    public static void SetAssetQueueSnapshot(long ready, long delayed, long active, long oldestMilliseconds)
    {
        Volatile.Write(ref RobloxMetrics.AssetRenderReady, ready);
        Volatile.Write(ref RobloxMetrics.AssetRenderDelayed, delayed);
        Volatile.Write(ref RobloxMetrics.AssetRenderActive, active);
        Volatile.Write(ref RobloxMetrics.AssetRenderOldestMilliseconds, oldestMilliseconds);
    }
}
