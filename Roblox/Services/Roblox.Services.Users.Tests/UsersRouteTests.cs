using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Dto.Users;
using Roblox.Models;
using Roblox.Services.Users.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Users.Tests;

public class UsersRouteTests
{
    private static readonly IReadOnlyList<UsersRouteCase> Routes = BuildRoutes();

    public static IEnumerable<object[]> AllRoutes()
    {
        return Routes.Select(route => new object[] { route });
    }

    public static IEnumerable<object[]> AuthenticatedRoutes()
    {
        return Routes
            .Where(route => route.RequiresSession)
            .Select(route => new object[] { route });
    }

    [Fact]
    public void RouteMatrix_CoversEveryUsersControllerRoute()
    {
        var declaredRoutes = DeclaredControllerRoutes();
        var matrixRoutes = Routes
            .Select(route => (route.Method, route.Path))
            .ToHashSet();

        var missing = declaredRoutes.Except(matrixRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();
        var extra = matrixRoutes.Except(declaredRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();

        Assert.True(missing.Count == 0, "Missing Users route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Users route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void RouteMatrix_MatchesEndpointMetadata()
    {
        var controllerRequiresInternalService = typeof(UsersController).GetCustomAttribute<InternalServiceOnlyAttribute>() != null;
        var declaredRoutes = typeof(UsersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method =>
            {
                var requiresSession = method.GetCustomAttribute<RequireRobloxSessionAttribute>() != null;
                var allowsAnonymous = method.GetCustomAttribute<AllowRobloxAnonymousAttribute>() != null;
                var requiresCsrf = method.GetCustomAttribute<RequireRobloxCsrfAttribute>() != null;
                return ExpandedMethodRoutes(method)
                    .Select(route => (
                        route.Method,
                        route.Path,
                        RequiresInternalService: controllerRequiresInternalService,
                        RequiresSession: requiresSession,
                        AllowsAnonymous: allowsAnonymous,
                        RequiresCsrf: requiresCsrf));
            })
            .ToDictionary(route => (route.Method, route.Path));

        foreach (var route in Routes)
        {
            Assert.True(declaredRoutes.TryGetValue((route.Method, route.Path), out var declared), $"Route {route} is not declared.");
            Assert.Equal(route.RequiresInternalService, declared.RequiresInternalService);
            Assert.Equal(route.RequiresSession, declared.RequiresSession);
            Assert.Equal(route.AllowsAnonymous, declared.AllowsAnonymous);
            Assert.Equal(route.RequiresCsrf, declared.RequiresCsrf);
        }
    }

    [Fact]
    public void ControllerActions_DoNotReturnDynamicObjectOrAnonymousTypes()
    {
        var violations = typeof(UsersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Where(method => IsUntypedReturn(method.ReturnType))
            .Select(method => $"{method.Name}: {method.ReturnType}")
            .ToList();

        Assert.True(violations.Count == 0, "Untyped Users controller action returns: " + string.Join(", ", violations));
    }

    [Theory]
    [MemberData(nameof(AllRoutes))]
    public async Task InternalRoutes_RejectRequestsWithoutProxyAuthorization(UsersRouteCase route)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(new HttpMethod(route.Method), route.SamplePath, includeProxyAuthorization: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AuthenticatedRoutes))]
    public async Task SessionRoutes_RejectAnonymousRequests(UsersRouteCase route)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(new HttpMethod(route.Method), route.SamplePath, authenticated: false, jsonBody: route.JsonBody);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task AuthenticatedRoute_ReturnsSessionShape(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(HttpMethod.Get, $"{prefix}/users/authenticated", authenticated: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(1, json.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("UsersRouteAuthenticated", json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.TryGetProperty("permissions", out var permissions));
        Assert.True(permissions.ValueKind == JsonValueKind.Array);
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task MultiGetUsersById_ReturnsCollectionShape(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.SendAsync(HttpMethod.Post, $"{prefix}/users", jsonBody: new MultiGetRequest
        {
            userIds = new[] { user.UserId },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var entry = json.RootElement.GetProperty("data")[0];
        Assert.Equal(user.UserId, entry.GetProperty("id").GetInt64());
        Assert.Equal(user.Username, entry.GetProperty("name").GetString());
        Assert.Equal(user.Username, entry.GetProperty("displayName").GetString());
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task MultiGetUsersById_RejectsInvalidCounts(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.SendAsync(HttpMethod.Post, $"{prefix}/users", jsonBody: new MultiGetRequest
        {
            userIds = Array.Empty<long>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task MultiGetUsersByUsername_ReturnsCollectionShape(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.SendAsync(HttpMethod.Post, $"{prefix}/usernames/users", jsonBody: new MultiGetByNameRequest
        {
            usernames = new[] { user.Username },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        var entry = json.RootElement.GetProperty("data")[0];
        Assert.Equal(user.UserId, entry.GetProperty("id").GetInt64());
        Assert.Equal(user.Username, entry.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("/v1", "GET")]
    [InlineData("/v1", "POST")]
    [InlineData("/apisite/users/v1", "GET")]
    [InlineData("/apisite/users/v1", "POST")]
    public async Task UserDetailsById_ReturnsLegacyShape(string prefix, string method)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.SendAsync(new HttpMethod(method), $"{prefix}/users/{user.UserId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(user.UserId, json.RootElement.GetProperty("id").GetInt64());
        Assert.Equal(user.Username, json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.TryGetProperty("inventoryRap", out _));
    }

    [Theory]
    [InlineData("/v1", "GET")]
    [InlineData("/v1", "POST")]
    [InlineData("/apisite/users/v1", "GET")]
    [InlineData("/apisite/users/v1", "POST")]
    public async Task UserDetailsByUsername_ReturnsStatsShape(string prefix, string method)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.SendAsync(new HttpMethod(method), $"{prefix}/users/{user.Username}/details");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.Equal(user.UserId, json.RootElement.GetProperty("id").GetInt64());
        Assert.Equal(user.Username, json.RootElement.GetProperty("name").GetString());
        Assert.True(json.RootElement.TryGetProperty("totalPlaceVisits", out _));
        Assert.True(json.RootElement.TryGetProperty("friendshipCount", out _));
        Assert.True(json.RootElement.TryGetProperty("inventoryRap", out _));
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task StatusRoutes_ReadNullThenUpdatedShape(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var before = await fixture.SendAsync(HttpMethod.Get, $"{prefix}/users/{user.UserId}/status");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        using (var beforeJson = await ReadJsonAsync(before))
        {
            Assert.Equal(JsonValueKind.Null, beforeJson.RootElement.GetProperty("status").ValueKind);
        }

        var patch = await fixture.SendAsync(
            HttpMethod.Patch,
            $"{prefix}/users/{user.UserId}/status",
            authenticated: true,
            jsonBody: new SetStatusRequest { status = "hello world" },
            authenticatedUser: user);
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var after = await fixture.SendAsync(HttpMethod.Get, $"{prefix}/users/{user.UserId}/status");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        using var afterJson = await ReadJsonAsync(after);
        Assert.Equal("hello world", afterJson.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/v1")]
    [InlineData("/apisite/users/v1")]
    public async Task PreviousUsernames_ReturnsLegacyNameShape(string prefix)
    {
        await using var fixture = await UsersRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.SendAsync(HttpMethod.Get, $"{prefix}/users/{user.UserId}/username-history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await ReadJsonAsync(response);
        Assert.True(json.RootElement.TryGetProperty("data", out var data));
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        if (data.GetArrayLength() > 0)
        {
            Assert.True(data[0].TryGetProperty("name", out _));
        }
    }

    [Fact]
    public async Task DockerInfrastructure_IsAvailableWhenRequired()
    {
        if (!UsersRouteTestFixture.DockerTestsRequired)
        {
            return;
        }

        Assert.True(await UsersRouteTestFixture.IsInfrastructureAvailableAsync());
    }

    private static IReadOnlyList<UsersRouteCase> BuildRoutes()
    {
        var routes = new List<UsersRouteCase>();
        foreach (var prefix in new[] { "/v1", "/apisite/users/v1" })
        {
            routes.Add(new("GET", $"{prefix}/users/authenticated", true, false, true, false, $"{prefix}/users/authenticated"));
            routes.Add(new("POST", $"{prefix}/users/{{username}}/details", false, true, true, false, $"{prefix}/users/example/details"));
            routes.Add(new("GET", $"{prefix}/users/{{username}}/details", false, true, true, false, $"{prefix}/users/example/details"));
            routes.Add(new("POST", $"{prefix}/users/{{userId:long}}", false, true, true, false, $"{prefix}/users/1"));
            routes.Add(new("GET", $"{prefix}/users/{{userId:long}}", false, true, true, false, $"{prefix}/users/1"));
            routes.Add(new("POST", $"{prefix}/users", false, true, true, false, $"{prefix}/users", new MultiGetRequest { userIds = new[] { 1L } }));
            routes.Add(new("POST", $"{prefix}/usernames/users", false, true, true, false, $"{prefix}/usernames/users", new MultiGetByNameRequest { usernames = new[] { "ROBLOX" } }));
            routes.Add(new("GET", $"{prefix}/users/{{userId:long}}/status", false, true, true, false, $"{prefix}/users/1/status"));
            routes.Add(new("PATCH", $"{prefix}/users/{{userId:long}}/status", true, false, true, true, $"{prefix}/users/1/status", new SetStatusRequest { status = "hello world" }));
            routes.Add(new("GET", $"{prefix}/users/{{userId:long}}/username-history", false, true, true, false, $"{prefix}/users/1/username-history"));
        }

        return routes;
    }

    private static HashSet<(string Method, string Path)> DeclaredControllerRoutes()
    {
        return typeof(UsersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(ExpandedMethodRoutes)
            .ToHashSet();
    }

    private static IEnumerable<(string Method, string Path)> ExpandedMethodRoutes(MethodInfo method)
    {
        var prefixes = typeof(UsersController)
            .GetCustomAttributes<RouteAttribute>()
            .Select(route => NormalizeRoute(route.Template ?? string.Empty));

        return method.GetCustomAttributes<HttpMethodAttribute>()
            .SelectMany(attribute => attribute.Template == null
                ? Enumerable.Empty<(string Method, string Path)>()
                : attribute.HttpMethods.SelectMany(httpMethod => prefixes.Select(prefix => (
                    Method: httpMethod.ToUpperInvariant(),
                    Path: CombineRoute(prefix, attribute.Template)))));
    }

    private static string CombineRoute(string prefix, string template)
    {
        return NormalizeRoute(prefix.TrimEnd('/') + "/" + template.TrimStart('/'));
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    private static bool IsUntypedReturn(Type returnType)
    {
        if (returnType == typeof(Task))
        {
            return false;
        }

        var type = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;

        return type == typeof(object) || IsAnonymousType(type);
    }

    private static bool IsAnonymousType(Type type)
    {
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false) &&
               type.IsGenericType &&
               type.Name.Contains("AnonymousType", StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    public sealed record UsersRouteCase(
        string Method,
        string Path,
        bool RequiresSession,
        bool AllowsAnonymous,
        bool RequiresInternalService,
        bool RequiresCsrf,
        string SamplePath,
        object? JsonBody = null)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }
}
