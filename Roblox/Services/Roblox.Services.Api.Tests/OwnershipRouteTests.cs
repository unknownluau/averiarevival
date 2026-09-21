using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class OwnershipRouteTests
{
    private static readonly IReadOnlyList<OwnershipRouteCase> Routes = new List<OwnershipRouteCase>
    {
        new("GET", "/ownership/hasasset"),
    };

    [Fact]
    public void RouteMatrix_CoversEveryOwnershipControllerRoute()
    {
        var declaredRoutes = typeof(OwnershipController)
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

        Assert.True(missing.Count == 0, "Missing Ownership route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Ownership route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void HasAssetRoute_IsBrowserFacingByDefaultAndAnonymousDocumented()
    {
        var method = typeof(OwnershipController).GetMethod(nameof(OwnershipController.HasAsset));

        Assert.NotNull(method);
        var concreteMethod = method!;
        Assert.NotNull(concreteMethod.GetCustomAttribute<AllowRobloxAnonymousAttribute>());
        Assert.Null(concreteMethod.GetCustomAttribute<RequireRobloxSessionAttribute>());
        Assert.Null(concreteMethod.GetCustomAttribute<RequireRccRequestAttribute>());
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    public sealed record OwnershipRouteCase(string Method, string Path);
}
