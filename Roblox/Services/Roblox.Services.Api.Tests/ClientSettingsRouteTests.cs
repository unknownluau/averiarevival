using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class ClientSettingsRouteTests
{
    private static readonly IReadOnlyList<ClientSettingsRouteCase> Routes = new List<ClientSettingsRouteCase>
    {
        new("POST", "/Setting/Get/{type}"),
        new("POST", "/Setting/QuietGet/{type}"),
        new("GET", "/Setting/Get/{type}"),
        new("GET", "/Setting/QuietGet/{type}"),
    };

    public static IEnumerable<object[]> LegacyClientSettingsRoutes()
    {
        return Routes.Select(route => new object[] { route });
    }

    [Fact]
    public void RouteMatrix_CoversEveryClientSettingsControllerRoute()
    {
        var declaredRoutes = typeof(ClientSettingsController)
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

        Assert.True(missing.Count == 0, "Missing ClientSettings route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "ClientSettings route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void LegacyClientSettingsRoutes_AreAnonymousDocumentedAndUnprotected()
    {
        var method = typeof(ClientSettingsController).GetMethod(nameof(ClientSettingsController.GetApplicationSettingsLegacy));

        Assert.NotNull(method);
        var concreteMethod = method!;
        Assert.NotNull(concreteMethod.GetCustomAttribute<AllowRobloxAnonymousAttribute>());
        Assert.Null(concreteMethod.GetCustomAttribute<RequireRobloxSessionAttribute>());
        Assert.Null(concreteMethod.GetCustomAttribute<RequireRccRequestAttribute>());
    }

    [Theory]
    [MemberData(nameof(LegacyClientSettingsRoutes))]
    public async Task LegacyClientSettingsRoutes_ReturnFeatureFlagsAnonymously(ClientSettingsRouteCase route)
    {
        await using var fixture = await CreateFixtureAsync();
        if (fixture == null)
        {
            return;
        }

        var path = route.Path.Replace("{type}", "StudioAppSettings", StringComparison.Ordinal);
        var request = new HttpRequestMessage(new HttpMethod(route.Method), path + "?apiKey=D6925E56-BFB9-4908-AAA2-A5B1EC4B2D79");
        if (route.Method == "POST")
        {
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"FFlagFromTest\":true}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LegacyClientSettingsRoute_RejectsInvalidApiKey()
    {
        await using var fixture = await CreateFixtureAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.GetAsync("/Setting/Get/StudioAppSettings?apiKey=bad-key");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    public sealed record ClientSettingsRouteCase(string Method, string Path)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }

    private static async Task<ApiRouteTestFixture?> CreateFixtureAsync()
    {
        var jsonDataDirectory = CreateJsonDataDirectory();
        Roblox.Configuration.JsonDataDirectory = jsonDataDirectory;

        return await ApiRouteTestFixture.CreateAsync(new Dictionary<string, string?>
        {
            ["Directories:JsonData"] = jsonDataDirectory,
        });
    }

    private static string CreateJsonDataDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "averia-api-client-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "StudioAppSettings.json"), "{\"FFlagFromTest\":true}");
        return directory;
    }
}
