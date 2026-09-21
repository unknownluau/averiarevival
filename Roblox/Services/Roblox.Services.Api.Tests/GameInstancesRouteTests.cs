using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class GameInstancesRouteTests
{
    private static readonly IReadOnlyList<GameInstancesRouteCase> Routes = new List<GameInstancesRouteCase>
    {
        new("GET", "/v1/Close"),
        new("POST", "/V1/Close"),
        new("POST", "/v2/CreateOrUpdate"),
        new("GET", "/v2/CreateOrUpdate"),
        new("GET", "/v1/CreateOrUpdate"),
        new("POST", "/v1/CreateOrUpdate"),
        new("POST", "/v1.0/Refresh"),
        new("POST", "/v2.0/Refresh"),
        new("GET", "/v1.0/Refresh"),
        new("GET", "/v2.0/Refresh"),
    };

    public static IEnumerable<object[]> GameInstanceRoutes()
    {
        return Routes.Select(route => new object[] { route });
    }

    [Fact]
    public void RouteMatrix_CoversEveryGameInstancesControllerRoute()
    {
        var declaredRoutes = typeof(GameInstancesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.Template == null
                ? Enumerable.Empty<(string Method, string Path)>()
                : attribute.HttpMethods.Select(method => (Method: method.ToUpperInvariant(), Path: NormalizeRoute(attribute.Template))))
            .ToHashSet();

        var matrixRoutes = Routes
            .Select(route => (Method: route.Method, Path: route.Path))
            .ToHashSet();

        var missing = declaredRoutes.Except(matrixRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();
        var extra = matrixRoutes.Except(declaredRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();

        Assert.True(missing.Count == 0, "Missing GameInstances route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "GameInstances route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void RouteMatrix_AllGameInstancesRoutesRequireRcc()
    {
        var unprotectedRoutes = typeof(GameInstancesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<RequireRccRequestAttribute>() == null)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .Select(attribute => NormalizeRoute(attribute.Template ?? string.Empty))
            .ToList();

        Assert.Empty(unprotectedRoutes);
    }

    [Theory]
    [MemberData(nameof(GameInstanceRoutes))]
    public async Task GameInstanceRoutes_RejectAnonymousRequests(GameInstancesRouteCase route)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(route.Method), route.Path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    public sealed record GameInstancesRouteCase(string Method, string Path)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }
}
