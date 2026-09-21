using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class UsersRouteTests
{
    private static readonly IReadOnlyList<UsersRouteCase> Routes = new List<UsersRouteCase>
    {
        new("GET", "/users/account-info", true),
        new("POST", "/users/account-info", true),
        new("GET", "/users/get-by-username", false),
        new("GET", "/users/{userId:long}", false),
        new("GET", "/users/{userId:long}/canmanage/{placeId:long}", false),
        new("POST", "/users/filter-friends", false),
        new("GET", "/game/players/{userId:long}", false),
    };

    public static IEnumerable<object[]> AccountInfoRoutes()
    {
        return Routes
            .Where(route => route.RequiresSession)
            .Select(route => new object[] { route });
    }

    [Fact]
    public void RouteMatrix_CoversEveryUsersControllerRoute()
    {
        var declaredRoutes = typeof(UsersController)
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

        Assert.True(missing.Count == 0, "Missing Users route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Users route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void RouteMatrix_MatchesExplicitSessionMetadata()
    {
        var declaredRoutes = typeof(UsersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method =>
            {
                var requiresSession = method.GetCustomAttribute<RequireRobloxSessionAttribute>() != null;
                return method.GetCustomAttributes<HttpMethodAttribute>()
                    .SelectMany(attribute => attribute.Template == null
                        ? Enumerable.Empty<(string Method, string Path, bool RequiresSession)>()
                        : attribute.HttpMethods.Select(httpMethod => (
                            Method: httpMethod.ToUpperInvariant(),
                            Path: NormalizeRoute(attribute.Template),
                            RequiresSession: requiresSession)));
            })
            .ToDictionary(route => (route.Method, route.Path));

        foreach (var route in Routes)
        {
            Assert.True(declaredRoutes.TryGetValue((route.Method, route.Path), out var declared), $"Route {route} is not declared.");
            Assert.Equal(route.RequiresSession, declared.RequiresSession);
        }
    }

    [Theory]
    [MemberData(nameof(AccountInfoRoutes))]
    public async Task AccountInfoRoutes_RejectAnonymousRequests(UsersRouteCase route)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(route.Method), route.Path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(1, "whitelist")]
    [InlineData(2, "blacklist")]
    public async Task GamePlayersRoute_ReturnsChatFilterForOwnerStatus(long userId, string expectedChatFilter)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.GetAsync($"/game/players/{userId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(expectedChatFilter, json.RootElement.GetProperty("ChatFilter").GetString());
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    public sealed record UsersRouteCase(string Method, string Path, bool RequiresSession)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }
}
