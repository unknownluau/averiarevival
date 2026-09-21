using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Avatar.Controllers;

namespace Roblox.Services.Avatar.Tests;

public sealed record AvatarRouteCase(
    string Method,
    string Path,
    bool RequiresSession,
    Func<AvatarRouteTestFixture, Task<HttpContent?>>? CreateContent = null,
    Func<AvatarRouteTestFixture, Task>? Arrange = null)
{
    public override string ToString()
    {
        return $"{Method} {Path}";
    }
}

public static class AvatarRouteCases
{
    public static IReadOnlyList<AvatarRouteCase> All { get; } = new List<AvatarRouteCase>
    {
        Anonymous("GET", "/v1/avatar-fetch?userId={userId}&placeId=0"),
        Anonymous("GET", "/v1.1/avatar-fetch?userId={userId}&placeId=0"),
        Anonymous("GET", "/v1/users/{userId}/outfits?itemsPerPage=10&page=1"),
        Anonymous("GET", "/apisite/avatar/v1/users/{userId}/outfits?itemsPerPage=10&page=1"),
        Anonymous("GET", "/v1/users/{userId}/avatar"),
        Anonymous("GET", "/apisite/avatar/v1/users/{userId}/avatar"),
        Anonymous("GET", "/v1/avatar", fixture =>
        {
            fixture.AddCookie("USERID", fixture.UserId.ToString());
            return Task.CompletedTask;
        }),
        Anonymous("GET", "/apisite/avatar/v1/avatar", fixture =>
        {
            fixture.AddCookie("USERID", fixture.UserId.ToString());
            return Task.CompletedTask;
        }),
        Anonymous("GET", "/v1/avatar/metadata"),
        Anonymous("GET", "/apisite/avatar/v1/avatar/metadata"),
        Anonymous("GET", "/v1/avatar-rules"),
        Anonymous("GET", "/apisite/avatar/v1/avatar-rules"),

        Session("POST", "/v1/avatar/redraw-thumbnail"),
        Session("POST", "/apisite/avatar/v1/avatar/redraw-thumbnail"),
        Session("POST", "/v1/avatar/set-wearing-assets", content: _ => JsonContent(new { assetIds = Array.Empty<long>() })),
        Session("POST", "/apisite/avatar/v1/avatar/set-wearing-assets", content: _ => JsonContent(new { assetIds = Array.Empty<long>() })),
        Session("POST", "/v1/avatar/assets/{assetId}/wear"),
        Session("POST", "/apisite/avatar/v1/avatar/assets/{assetId}/wear"),
        Session("POST", "/v1/avatar/assets/{assetId}/remove"),
        Session("POST", "/apisite/avatar/v1/avatar/assets/{assetId}/remove"),
        Session("POST", "/v1/avatar/set-scales", content: _ => JsonContent(new
        {
            height = 1.0,
            width = 1.0,
            head = 1.0,
            depth = 1.0,
            proportion = 0.0,
            bodyType = 0.0,
        })),
        Session("POST", "/apisite/avatar/v1/avatar/set-scales", content: _ => JsonContent(new
        {
            height = 1.0,
            width = 1.0,
            head = 1.0,
            depth = 1.0,
            proportion = 0.0,
            bodyType = 0.0,
        })),
        Session("POST", "/v1/avatar/set-player-avatar-type", content: _ => JsonContent(new { playerAvatarType = 1 })),
        Session("POST", "/apisite/avatar/v1/avatar/set-player-avatar-type", content: _ => JsonContent(new { playerAvatarType = 1 })),
        Session("POST", "/v1/avatar/set-body-colors", content: _ => JsonContent(new
        {
            headColorId = 194,
            torsoColorId = 23,
            leftArmColorId = 194,
            rightArmColorId = 194,
            leftLegColorId = 102,
            rightLegColorId = 102,
        })),
        Session("POST", "/apisite/avatar/v1/avatar/set-body-colors", content: _ => JsonContent(new
        {
            headColorId = 194,
            torsoColorId = 23,
            leftArmColorId = 194,
            rightArmColorId = 194,
            leftLegColorId = 102,
            rightLegColorId = 102,
        })),
        Session("GET", "/v1/recent-items/{recentType}/list"),
        Session("GET", "/apisite/avatar/v1/recent-items/{recentType}/list"),
        Session("POST", "/v1/outfits/{outfitId}/wear", arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("POST", "/apisite/avatar/v1/outfits/{outfitId}/wear", arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("POST", "/v1/outfits/create", content: _ => JsonContent(new { name = "RouteTest" })),
        Session("POST", "/apisite/avatar/v1/outfits/create", content: _ => JsonContent(new { name = "RouteTest" })),
        Session("POST", "/v1/outfits/{outfitId}/delete", arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("POST", "/apisite/avatar/v1/outfits/{outfitId}/delete", arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("POST", "/v1/outfits/{outfitId}/rename", content: _ => JsonContent(new { name = "Renamed" }), arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("POST", "/apisite/avatar/v1/outfits/{outfitId}/rename", content: _ => JsonContent(new { name = "Renamed" }), arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("PATCH", "/v1/outfits/{outfitId}", content: _ => JsonContent(new { name = "Updated" }), arrange: fixture => fixture.EnsureOutfitAsync()),
        Session("PATCH", "/apisite/avatar/v1/outfits/{outfitId}", content: _ => JsonContent(new { name = "Updated" }), arrange: fixture => fixture.EnsureOutfitAsync()),
    };

    public static IEnumerable<object[]> PublicRoutes()
    {
        return All.Where(route => !route.RequiresSession).Select(route => new object[] { route });
    }

    public static IEnumerable<object[]> SessionRoutes()
    {
        return All.Where(route => route.RequiresSession).Select(route => new object[] { route });
    }

    public static IEnumerable<object[]> AllRoutes()
    {
        return All.Select(route => new object[] { route });
    }

    public static IReadOnlySet<(string Method, string Path)> DeclaredControllerRoutes()
    {
        return typeof(AvatarController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.Template == null
                ? Enumerable.Empty<(string Method, string Path)>()
                : attribute.HttpMethods.Select(method => (method.ToUpperInvariant(), NormalizeTemplate(attribute.Template))))
            .ToHashSet();
    }

    public static IReadOnlySet<(string Method, string Path)> MatrixRoutes()
    {
        return All
            .Select(route => (route.Method.ToUpperInvariant(), NormalizeMatrixTemplate(route.Path)))
            .ToHashSet();
    }

    private static AvatarRouteCase Anonymous(string method, string path, Func<AvatarRouteTestFixture, Task>? arrange = null)
    {
        return new AvatarRouteCase(method, path, false, Arrange: arrange);
    }

    private static AvatarRouteCase Session(
        string method,
        string path,
        Func<AvatarRouteTestFixture, Task<HttpContent?>>? content = null,
        Func<AvatarRouteTestFixture, Task>? arrange = null)
    {
        return new AvatarRouteCase(method, path, true, content, arrange);
    }

    private static Task<HttpContent?> JsonContent(object value)
    {
        return Task.FromResult<HttpContent?>(System.Net.Http.Json.JsonContent.Create(value));
    }

    private static string NormalizeTemplate(string template)
    {
        var withoutConstraints = template
            .Replace("{assetId:long}", "{assetId}", StringComparison.Ordinal)
            .Replace("{outfitId:long}", "{outfitId}", StringComparison.Ordinal)
            .Replace("{userId:long}", "{userId}", StringComparison.Ordinal);

        return withoutConstraints.StartsWith('/') ? withoutConstraints : "/" + withoutConstraints;
    }

    private static string NormalizeMatrixTemplate(string path)
    {
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        return path;
    }
}
