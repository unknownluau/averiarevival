namespace Roblox.Rendering;

public sealed class AvatarBodyColors
{
    public int headColorId { get; set; }
    public int torsoColorId { get; set; }
    public int rightArmColorId { get; set; }
    public int leftArmColorId { get; set; }
    public int rightLegColorId { get; set; }
    public int leftLegColorId { get; set; }
}

public sealed class AvatarAssetTypeEntry
{
    public int id { get; set; }
}

public sealed class AvatarAssetEntry
{
    public long id { get; set; }
    public AvatarAssetTypeEntry? assetType { get; set; }
}

public sealed class AvatarData
{
    public long userId { get; set; }
    public AvatarBodyColors? bodyColors { get; set; }
    public string? playerAvatarType { get; set; }
    public IEnumerable<AvatarAssetEntry>? assets { get; set; }
}
