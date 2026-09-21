using System.Text.Json;

namespace Roblox.Services.Api.Tests;

public class ApiProxyConfigurationTests
{
    [Fact]
    public void ApiProxyConfig_RoutesApiHostToApiClusterOnly()
    {
        using var document = LoadApiProxyAppSettings();
        var root = document.RootElement;
        var routes = root.GetProperty("ReverseProxy").GetProperty("Routes");
        var clusters = root.GetProperty("ReverseProxy").GetProperty("Clusters");

        Assert.True(routes.TryGetProperty("api-host-route", out var apiRoute), "api-host-route should exist.");
        Assert.Equal("api-cluster", apiRoute.GetProperty("ClusterId").GetString());
        Assert.Equal("{**catch-all}", apiRoute.GetProperty("Match").GetProperty("Path").GetString());
        var apiHosts = ReadStringArray(apiRoute.GetProperty("Match").GetProperty("Hosts"));
        Assert.Contains("api.averia.lol", apiHosts);
        Assert.Contains("*.api.averia.lol", apiHosts);

        Assert.True(clusters.TryGetProperty("api-cluster", out var apiCluster), "api-cluster should exist.");
        Assert.Equal(
            "http://127.0.0.1:5204",
            apiCluster.GetProperty("Destinations").GetProperty("primary").GetProperty("Address").GetString());

        Assert.False(clusters.TryGetProperty("moderation-cluster", out _), "moderation-cluster should be renamed.");
        Assert.False(routes.TryGetProperty("moderation-route", out _), "Path-only moderation-route should be removed.");
        Assert.False(routes.TryGetProperty("moderation-assetgame-route", out _), "assetgame moderation route should be removed.");

        foreach (var route in routes.EnumerateObject())
        {
            var match = route.Value.GetProperty("Match");
            if (match.TryGetProperty("Path", out var path))
            {
                Assert.NotEqual("/moderation/{**catch-all}", path.GetString());
            }

            if (match.TryGetProperty("Hosts", out var hosts))
            {
                Assert.DoesNotContain("assetgame.averia.lol", ReadStringArray(hosts));
            }
        }
    }

    [Fact]
    public void ApiProxyConfig_UsesApiHostAsInternalServiceHostAndNoModerationPathOnlyInternalRoute()
    {
        using var document = LoadApiProxyAppSettings();
        var root = document.RootElement;

        Assert.Contains("api.averia.lol", ReadStringArray(root.GetProperty("InternalServiceHosts")));
        Assert.Contains("*.api.averia.lol", ReadStringArray(root.GetProperty("InternalServiceHosts")));

        foreach (var route in root.GetProperty("InternalServiceRoutes").EnumerateArray())
        {
            if (!route.TryGetProperty("PathPrefixes", out var prefixes))
            {
                continue;
            }

            Assert.DoesNotContain("/moderation/", ReadStringArray(prefixes));
            Assert.DoesNotContain("/marketplace/", ReadStringArray(prefixes));
            Assert.DoesNotContain("/gametransactions/", ReadStringArray(prefixes));
        }
    }

    [Fact]
    public void ApiProxyConfig_DoesNotPathRouteMarketplaceOrGameTransactions()
    {
        using var document = LoadApiProxyAppSettings();
        var routes = document.RootElement.GetProperty("ReverseProxy").GetProperty("Routes");

        foreach (var route in routes.EnumerateObject())
        {
            var match = route.Value.GetProperty("Match");
            if (!match.TryGetProperty("Path", out var path))
            {
                continue;
            }

            Assert.DoesNotContain("/marketplace/", path.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/gametransactions/", path.GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ApiProxyConfig_DeclaresFrontendPublicHosts()
    {
        using var document = LoadApiProxyAppSettings();
        var frontendProxy = document.RootElement.GetProperty("FrontendProxy");

        var hosts = ReadStringArray(frontendProxy.GetProperty("PublicHosts"));
        Assert.Contains("averia.lol", hosts);
        Assert.Contains("www.averia.lol", hosts);
    }

    [Fact]
    public void ApiProxyConfig_RoutesAdminHostV1ToAdminCluster()
    {
        using var document = LoadApiProxyAppSettings();
        var root = document.RootElement;
        var routes = root.GetProperty("ReverseProxy").GetProperty("Routes");
        var clusters = root.GetProperty("ReverseProxy").GetProperty("Clusters");

        Assert.Contains("admin.averia.lol", ReadStringArray(root.GetProperty("InternalServiceHosts")));

        Assert.True(routes.TryGetProperty("admin-host-route", out var adminRoute), "admin-host-route should exist.");
        Assert.Equal("admin-cluster", adminRoute.GetProperty("ClusterId").GetString());
        Assert.Equal("/v1/{**catch-all}", adminRoute.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Contains("admin.averia.lol", ReadStringArray(adminRoute.GetProperty("Match").GetProperty("Hosts")));

        Assert.True(clusters.TryGetProperty("admin-cluster", out var adminCluster), "admin-cluster should exist.");
        Assert.Equal(
            "http://127.0.0.1:5210",
            adminCluster.GetProperty("Destinations").GetProperty("primary").GetProperty("Address").GetString());

        Assert.False(routes.TryGetProperty("admin-api-route", out _), "legacy /admin-api/api public proxy route should not exist.");
    }

    private static JsonDocument LoadApiProxyAppSettings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Roblox.ApiProxy", "appsettings.json");
            if (File.Exists(candidate))
            {
                return JsonDocument.Parse(File.ReadAllText(candidate));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find Roblox.ApiProxy/appsettings.json for proxy configuration tests.");
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement array)
    {
        return array.EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToList();
    }
}
