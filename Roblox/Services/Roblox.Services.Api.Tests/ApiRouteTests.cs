using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;

namespace Roblox.Services.Api.Tests;

public class ApiRouteTests
{
    public static IEnumerable<object[]> FilterTextRoutes()
    {
        yield return new object[] { "/moderation/v2/filtertext/" };
        yield return new object[] { "/moderation/filtertext/" };
    }

    [Theory]
    [MemberData(nameof(FilterTextRoutes))]
    public async Task FilterTextRoutes_ReturnExpectedResponseShapeAnonymously(string path)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["text"] = "hello world",
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.Equal("hello world", data.GetProperty("AgeUnder13").GetString());
        Assert.Equal("hello world", data.GetProperty("Age13OrOver").GetString());
        Assert.Equal("hello world", data.GetProperty("white").GetString());
        Assert.Equal("hello world", data.GetProperty("black").GetString());
    }

    [Fact]
    public void RouteMatrix_CoversEveryControllerRoute()
    {
        var declaredRoutes = typeof(ModerationController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.Template == null
                ? Enumerable.Empty<(string Method, string Path)>()
                : attribute.HttpMethods.Select(method => (Method: method.ToUpperInvariant(), Path: NormalizeRoute(attribute.Template))))
            .ToHashSet();

        var matrixRoutes = FilterTextRoutes()
            .Select(route => (Method: "POST", Path: NormalizeRoute((string)route[0])))
            .ToHashSet();

        var missing = declaredRoutes.Except(matrixRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();
        var extra = matrixRoutes.Except(declaredRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();

        Assert.True(missing.Count == 0, "Missing API route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "API route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }
}
