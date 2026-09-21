using System.Text;
using Roblox.Services;

namespace Roblox.UnitTest;

public sealed class ImageThumbnailRoutingTests
{
    // A valid 1x1 PNG. Raw uploaded images should be handled locally without RCC.
    private const string Png =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public async Task RawPng_IsDirectlyDecodable()
    {
        await using var content = new MemoryStream(Convert.FromBase64String(Png));

        Assert.True(await AssetsService.IsDirectlyDecodableImageAsync(content, TestContext.Current.CancellationToken));
        Assert.Equal(0, content.Position);
    }

    [Fact]
    public async Task RobloxXmlObject_RequiresRccFallback()
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(
            "<roblox><Item class=\"Decal\"><Properties><Content name=\"Texture\"><url>rbxassetid://1</url></Content></Properties></Item></roblox>"));

        Assert.False(await AssetsService.IsDirectlyDecodableImageAsync(content, TestContext.Current.CancellationToken));
        Assert.Equal(0, content.Position);
    }

    [Fact]
    public async Task UnsupportedImageFormat_RequiresRccFallback()
    {
        // GIF is parseable by libvips, but the upload contract only treats PNG/JPEG as
        // directly renderable image content.
        var gif = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==");
        await using var content = new MemoryStream(gif);

        Assert.False(await AssetsService.IsDirectlyDecodableImageAsync(content, TestContext.Current.CancellationToken));
        Assert.Equal(0, content.Position);
    }
}
