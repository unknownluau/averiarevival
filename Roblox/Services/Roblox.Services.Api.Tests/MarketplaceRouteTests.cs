using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class MarketplaceRouteTests
{
    private static readonly IReadOnlyList<MarketplaceRouteCase> Routes = new List<MarketplaceRouteCase>
    {
        new("GET", "/marketplace/productinfo", false, false),
        new("POST", "/marketplace/submitpurchase", true, false),
        new("POST", "/marketplace/purchase", true, false),
        new("GET", "/marketplace/productdetails", false, false),
        new("GET", "/marketplace/game-pass-product-info", false, false),
        new("POST", "/marketplace/validatepurchase", false, true),
        new("GET", "/gametransactions/getpendingtransactions", false, true),
        new("POST", "/gametransactions/settransactionstatuscomplete", false, true),
    };

    public static IEnumerable<object[]> ProtectedRoutes()
    {
        return Routes
            .Where(route => route.RequiresSession || route.RequiresRcc)
            .Select(route => new object[] { route });
    }

    [Fact]
    public void RouteMatrix_CoversEveryMarketplaceControllerRoute()
    {
        var declaredRoutes = typeof(MarketplaceController)
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

        Assert.True(missing.Count == 0, "Missing Marketplace route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Marketplace route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void RouteMatrix_MatchesExplicitAuthMetadata()
    {
        var declaredRoutes = typeof(MarketplaceController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method =>
            {
                var requiresSession = method.GetCustomAttribute<RequireRobloxSessionAttribute>() != null;
                var requiresRcc = method.GetCustomAttribute<RequireRccRequestAttribute>() != null;
                return method.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(attribute => attribute.Template == null
                        ? Enumerable.Empty<(string Method, string Path, bool RequiresSession, bool RequiresRcc)>()
                        : attribute.HttpMethods.Select(httpMethod => (
                            Method: httpMethod.ToUpperInvariant(),
                            Path: NormalizeRoute(attribute.Template),
                            RequiresSession: requiresSession,
                            RequiresRcc: requiresRcc)));
            })
            .ToDictionary(route => (route.Method, route.Path));

        foreach (var route in Routes)
        {
            Assert.True(declaredRoutes.TryGetValue((route.Method, route.Path), out var declared), $"Route {route} is not declared.");
            Assert.Equal(route.RequiresSession, declared.RequiresSession);
            Assert.Equal(route.RequiresRcc, declared.RequiresRcc);
        }
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task ProtectedMarketplaceRoutes_RejectAnonymousRequests(MarketplaceRouteCase route)
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

    public sealed record MarketplaceRouteCase(string Method, string Path, bool RequiresSession, bool RequiresRcc)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }
}
