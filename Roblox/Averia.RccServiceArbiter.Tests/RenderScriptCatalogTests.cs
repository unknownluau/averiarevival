using System.Text.Json;
using Averia.RccServiceArbiter.Configuration;
using Averia.RccServiceArbiter.Rendering;
using Microsoft.Extensions.Options;
using Roblox.Rendering;
using Xunit;

namespace Averia.RccServiceArbiter.Tests;

public sealed class RenderScriptCatalogTests
{
    [Theory]
    [InlineData(RenderKind.Avatar, "Avatar_R15_Action", "PNG")]
    [InlineData(RenderKind.Avatar3D, "Avatar_R15_Action", "OBJ")]
    [InlineData(RenderKind.AvatarHeadshot, "Closeup", "PNG")]
    [InlineData(RenderKind.MeshPart, "MeshPart", "PNG")]
    [InlineData(RenderKind.Animation, "AvatarAnimation", "PNG")]
    public void ModernRcc_UsesJsonThumbnailPayload(RenderKind kind, string expectedType, string expectedFormat)
    {
        var catalog = CreateCatalog();
        var execution = catalog.Create(new RenderRequest
        {
            Kind = kind, AssetId = 123, UserId = 456, Width = 640, Height = 360,
            CharacterAppearanceUrl = "https://example.test/avatar", AnimationUrl = "https://example.test/animation",
        });
        using var document = JsonDocument.Parse(execution.Script);
        Assert.Equal("Thumbnail", document.RootElement.GetProperty("Mode").GetString());
        var settings = document.RootElement.GetProperty("Settings");
        Assert.Equal(expectedType, settings.GetProperty("Type").GetString());
        Assert.Contains(settings.GetProperty("Arguments").EnumerateArray(), value => value.ValueKind == JsonValueKind.String && value.GetString() == expectedFormat);
        Assert.Empty(execution.Arguments);
    }

    [Fact]
    public void R6BodyShot_UsesAvatarTypeAndR6ArgumentOrder()
    {
        var execution = CreateCatalog().Create(new RenderRequest
        {
            Kind = RenderKind.Avatar,
            UserId = 456,
            AvatarRigType = AvatarRigType.R6,
            CharacterAppearanceUrl = "https://example.test/avatar-r6",
            Width = 840,
            Height = 840,
        });
        using var document = JsonDocument.Parse(execution.Script);
        var settings = document.RootElement.GetProperty("Settings");
        Assert.Equal("Avatar", settings.GetProperty("Type").GetString());
        var arguments = settings.GetProperty("Arguments");
        Assert.Equal("https://example.test/avatar-r6", arguments[0].GetString());
        Assert.Equal("https://example.test/", arguments[1].GetString());
        Assert.Equal("PNG", arguments[2].GetString());
        Assert.Equal(840, arguments[3].GetInt32());
        Assert.Equal(840, arguments[4].GetInt32());
        Assert.Equal(5, arguments.GetArrayLength());
    }

    [Fact]
    public void R15BodyShot_UsesActionTypeAndR15ArgumentOrder()
    {
        var execution = CreateCatalog().Create(new RenderRequest
        {
            Kind = RenderKind.Avatar,
            UserId = 456,
            AvatarRigType = AvatarRigType.R15,
            CharacterAppearanceUrl = "https://example.test/avatar-r15",
            Width = 840,
            Height = 840,
        });
        using var document = JsonDocument.Parse(execution.Script);
        var settings = document.RootElement.GetProperty("Settings");
        Assert.Equal("Avatar_R15_Action", settings.GetProperty("Type").GetString());
        var arguments = settings.GetProperty("Arguments");
        Assert.Equal("https://example.test/", arguments[0].GetString());
        Assert.Equal("https://example.test/avatar-r15", arguments[1].GetString());
        Assert.Equal("PNG", arguments[2].GetString());
        Assert.Equal(10, arguments.GetArrayLength());
    }

    [Fact]
    public void PrivateOrigin_RewritesDependencyHostsAndAddsCorrelationId()
    {
        var catalog = new RenderScriptCatalog(Options.Create(new ArbiterOptions
        {
            BaseUrl = "https://public.example.test",
            Render = new ArbiterRenderOptions { DefaultYear = 2020, OriginBaseUrl = "http://10.0.0.20:8080" },
        }));
        var execution = catalog.Create(new RenderRequest
        {
            Kind = RenderKind.Avatar,
            UserId = 456,
            CharacterAppearanceUrl = "https://api.public.test/v1/avatar?userId=456",
            CorrelationId = "render-abc",
        });
        using var document = JsonDocument.Parse(execution.Script);
        var arguments = document.RootElement.GetProperty("Settings").GetProperty("Arguments");

        var appearance = new Uri(arguments[1].GetString()!);
        Assert.Equal("10.0.0.20", appearance.Host);
        Assert.Equal(8080, appearance.Port);
        Assert.Contains("renderCorrelationId=render-abc", appearance.Query);
    }

    [Theory]
    [InlineData("http://www.averia.lol")]
    [InlineData("http://www.averia.lol/")]
    public void PackageRender_NormalizesBaseUrlBeforeRccConcatenation(string configuredBaseUrl)
    {
        var catalog = new RenderScriptCatalog(Options.Create(new ArbiterOptions
        {
            BaseUrl = configuredBaseUrl,
            Render = new ArbiterRenderOptions { DefaultYear = 2020 },
        }));

        var execution = catalog.Create(new RenderRequest
        {
            Kind = RenderKind.Package,
            AssetUrls = "http://www.averia.lol/asset/?id=1",
        });
        using var document = JsonDocument.Parse(execution.Script);
        var arguments = document.RootElement.GetProperty("Settings").GetProperty("Arguments");

        Assert.Equal("http://www.averia.lol/", arguments[1].GetString());
        Assert.Equal("http://www.averia.lol/asset/?id=1785197", arguments[5].GetString());
    }

    [Fact]
    public void ModernTeeShirt_UsesRegisteredImageOperationWithUnderlyingContentId()
    {
        var catalog = new RenderScriptCatalog(Options.Create(new ArbiterOptions
        {
            BaseUrl = "https://example.test",
            Render = new ArbiterRenderOptions { DefaultYear = 2021 },
        }));

        var execution = catalog.Create(new RenderRequest
        {
            Kind = RenderKind.TeeShirt,
            AssetId = 901455,
            ContentId = 135483,
            Width = 420,
            Height = 420,
        });
        using var document = JsonDocument.Parse(execution.Script);
        var settings = document.RootElement.GetProperty("Settings");
        var arguments = settings.GetProperty("Arguments");

        Assert.Equal("Image", settings.GetProperty("Type").GetString());
        Assert.Equal(135483, arguments[0].GetInt64());
        Assert.Equal("https://example.test/", arguments[1].GetString());
        Assert.Equal("PNG", arguments[2].GetString());
        Assert.Equal(420, arguments[3].GetInt32());
        Assert.Equal(420, arguments[4].GetInt32());
        Assert.Equal(10, arguments.GetArrayLength());
    }

    private static RenderScriptCatalog CreateCatalog() => new(Options.Create(new ArbiterOptions
    { BaseUrl = "https://example.test", Render = new ArbiterRenderOptions { DefaultYear = 2020 } }));
}
