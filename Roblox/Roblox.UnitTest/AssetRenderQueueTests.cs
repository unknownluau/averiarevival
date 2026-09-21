using Microsoft.Extensions.Configuration;
using Roblox.Services.Assets;
using StackExchange.Redis;
using AssetType = Roblox.Models.Assets.Type;

namespace Roblox.UnitTest;

public sealed class AssetRenderQueueTests
{
    [Theory]
    [InlineData(AssetType.Hat, true)]
    [InlineData(AssetType.Image, true)]
    [InlineData(AssetType.Animation, true)]
    [InlineData(AssetType.Audio, false)]
    [InlineData(AssetType.Lua, false)]
    [InlineData(AssetType.Plugin, false)]
    [InlineData(AssetType.Place, false)]
    [InlineData(AssetType.Video, false)]
    [InlineData(AssetType.MeshPart, false)]
    public void RenderableAssetTypes_MatchRenderAssetImplementation(AssetType type, bool expected)
    {
        Assert.Equal(expected, AssetRenderQueue.IsRenderable(type));
    }

    [Fact]
    public void Configuration_BindsAndBoundsQueueSettings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Render:UseDurableAssetQueue"] = "true",
            ["Render:AssetQueue:FastLaneConcurrency"] = "3",
            ["Render:AssetQueue:ProcessingLeaseSeconds"] = "180",
            ["Render:AssetQueue:RetryDelaySeconds:0"] = "2",
            ["Render:AssetQueue:RetryDelaySeconds:1"] = "9",
        }).Build();

        AssetRenderQueue.Configure(configuration);

        Assert.True(AssetRenderQueue.Enabled);
        var options = AssetRenderQueue.GetOptions();
        Assert.Equal(3, options.FastLaneConcurrency);
        Assert.Equal(180, options.ProcessingLeaseSeconds);
        Assert.Equal([2, 9], options.RetryDelaySeconds);
    }

    [Fact]
    public void Identity_IsVersionAndRenderKindSpecific()
    {
        var first = new AssetRenderJob((RedisValue)"1-0", 10, 20, AssetType.Hat, "thumbnail", 0, 1);
        var newer = first with { AssetVersionId = 21 };
        var otherKind = first with { RenderKind = "icon" };

        Assert.NotEqual(AssetRenderQueue.DedupKey(first), AssetRenderQueue.DedupKey(newer));
        Assert.NotEqual(AssetRenderQueue.DedupKey(first), AssetRenderQueue.DedupKey(otherKind));
        Assert.Equal(AssetRenderQueue.LeaseKey(first), AssetRenderQueue.LeaseKey(first with { Attempt = 3 }));
    }
}
