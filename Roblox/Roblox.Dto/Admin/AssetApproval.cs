using Roblox.Models.Assets;

namespace Roblox.Dto.Admin;

public class PendingGroupIconEntry
{
    public long group_id { get; set; }
    public string name { get; set; } = string.Empty;
    public long user_id { get; set; }
    public string creatorName { get; set; } = string.Empty;
}

public class PendingAssetIconEntry
{
    public long id { get; set; }
    public string name { get; set; } = string.Empty;
    public string? content_url { get; set; }
    public long asset_id { get; set; }
    public long creatorId { get; set; }
    public string creatorName { get; set; } = string.Empty;
}

public class AssetModerationDetailsEntry : PendingAssetEntry
{
}
