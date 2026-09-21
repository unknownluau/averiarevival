namespace Roblox.Metrics;

public enum GroupFloodCheckType { WallPost, GlobalWallPost, GlobalWallPostForGroup }

public static class GroupMetrics
{
    public static void ReportFloodCheck(GroupFloodCheckType type) => RobloxMetrics.FloodChecks.Add(1,
        new KeyValuePair<string, object?>("flood_check.domain", "group"),
        new KeyValuePair<string, object?>("flood_check.type", type switch
        {
            GroupFloodCheckType.WallPost => "wall_post",
            GroupFloodCheckType.GlobalWallPost => "global_wall_post",
            GroupFloodCheckType.GlobalWallPostForGroup => "global_wall_post_for_group",
            _ => "unknown",
        }),
        new KeyValuePair<string, object?>("flood_check.scope", type == GroupFloodCheckType.WallPost ? "local" : "global"));
}
