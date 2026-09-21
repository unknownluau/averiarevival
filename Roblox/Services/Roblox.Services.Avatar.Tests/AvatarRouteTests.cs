using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Avatar.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Avatar.Tests;

public class AvatarRouteTests
{
    [Fact]
    public void AvatarRouteMatrix_CoversEveryControllerRoute()
    {
        var declaredRoutes = AvatarRouteCases.DeclaredControllerRoutes();
        var matrixRoutes = AvatarRouteCases.MatrixRoutes();

        var missing = declaredRoutes.Except(matrixRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();
        var extra = matrixRoutes.Except(declaredRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();

        Assert.True(missing.Count == 0, "Missing Avatar route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Avatar route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void AvatarRouteMatrix_MatchesEndpointAuthMetadata()
    {
        var controllerRoutes = typeof(AvatarController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method =>
            {
                var requiresSession = method.GetCustomAttribute<RequireRobloxSessionAttribute>() != null;
                return method.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(attribute => attribute.Template == null
                        ? Enumerable.Empty<(string Method, string Path, bool RequiresSession)>()
                        : attribute.HttpMethods.Select(httpMethod => (httpMethod.ToUpperInvariant(), NormalizeTemplate(attribute.Template), requiresSession)));
            })
            .ToDictionary(route => (route.Item1, route.Item2), route => route.Item3);

        foreach (var route in AvatarRouteCases.All)
        {
            var key = (route.Method.ToUpperInvariant(), NormalizeMatrixPath(route.Path));
            Assert.True(controllerRoutes.TryGetValue(key, out var requiresSession), $"Route {route} is not declared by AvatarController.");
            Assert.Equal(requiresSession, route.RequiresSession);
        }
    }

    [Theory]
    [MemberData(nameof(AvatarRouteCases.PublicRoutes), MemberType = typeof(AvatarRouteCases))]
    public async Task PublicRoutes_ReturnExpectedResponseShape(AvatarRouteCase route)
    {
        await using var fixture = await AvatarRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(route, authenticated: false);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected OK for {route}, got {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        await AssertResponseShapeAsync(route, response);
    }

    [Theory]
    [MemberData(nameof(AvatarRouteCases.SessionRoutes), MemberType = typeof(AvatarRouteCases))]
    public async Task SessionRoutes_RejectAnonymousRequests(AvatarRouteCase route)
    {
        await using var fixture = await AvatarRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(route, authenticated: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await ReadJsonAsync(response);
        Assert.True(json.RootElement.TryGetProperty("errors", out var errors), "Unauthorized response should contain errors.");
        Assert.True(errors.GetArrayLength() > 0, "Unauthorized response should include at least one error.");
    }

    [Theory]
    [MemberData(nameof(AvatarRouteCases.SessionRoutes), MemberType = typeof(AvatarRouteCases))]
    public async Task SessionRoutes_AcceptForwardedSessionRequests(AvatarRouteCase route)
    {
        await using var fixture = await AvatarRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(route, authenticated: true);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DockerInfrastructure_IsAvailableWhenRequired()
    {
        if (!AvatarRouteTestFixture.DockerTestsRequired)
        {
            return;
        }

        Assert.True(await AvatarRouteTestFixture.IsInfrastructureAvailableAsync());
    }

    private static async Task AssertResponseShapeAsync(AvatarRouteCase route, HttpResponseMessage response)
    {
        var json = await ReadJsonAsync(response);
        var root = json.RootElement;

        if (route.Path.Contains("/avatar/metadata", StringComparison.Ordinal))
        {
            Assert.True(root.TryGetProperty("enableDefaultClothingMessage", out _));
            Assert.True(root.TryGetProperty("supportProportionAndBodyType", out _));
            return;
        }

        if (route.Path.Contains("/avatar-rules", StringComparison.Ordinal))
        {
            Assert.True(root.TryGetProperty("playerAvatarTypes", out _));
            Assert.True(root.TryGetProperty("scales", out _));
            Assert.True(root.TryGetProperty("wearableAssetTypes", out _));
            return;
        }

        if (route.Path.Contains("/outfits", StringComparison.Ordinal))
        {
            Assert.True(root.TryGetProperty("data", out _));
            Assert.True(root.TryGetProperty("total", out _));
            return;
        }

        if (route.Path.Contains("/avatar-fetch", StringComparison.Ordinal))
        {
            Assert.True(root.TryGetProperty("resolvedAvatarType", out _));
            Assert.True(root.TryGetProperty("bodyColors", out _));
            Assert.True(root.TryGetProperty("scales", out _));
            return;
        }

        Assert.True(root.TryGetProperty("playerAvatarType", out _));
        Assert.True(root.TryGetProperty("bodyColors", out _));
        Assert.True(root.TryGetProperty("assets", out _));
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static string NormalizeTemplate(string template)
    {
        return template
            .Replace("{assetId:long}", "{assetId}", StringComparison.Ordinal)
            .Replace("{outfitId:long}", "{outfitId}", StringComparison.Ordinal)
            .Replace("{userId:long}", "{userId}", StringComparison.Ordinal);
    }

    private static string NormalizeMatrixPath(string path)
    {
        var queryIndex = path.IndexOf('?');
        return queryIndex < 0 ? path : path[..queryIndex];
    }
}
