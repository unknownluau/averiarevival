using System.Text.Json;

namespace Roblox.Services.Users.Tests;

public class ApiProxyUsersConfigurationTests
{
    [Fact]
    public void ApiProxyConfig_RoutesUsersHostAndApisitePathToUsersCluster()
    {
        using var document = LoadApiProxyAppSettings();
        var root = document.RootElement;
        var routes = root.GetProperty("ReverseProxy").GetProperty("Routes");
        var clusters = root.GetProperty("ReverseProxy").GetProperty("Clusters");

        Assert.True(routes.TryGetProperty("users-host-route", out var hostRoute), "users-host-route should exist.");
        Assert.Equal("users-cluster", hostRoute.GetProperty("ClusterId").GetString());
        Assert.Equal("{**catch-all}", hostRoute.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Contains("users.averia.lol", ReadStringArray(hostRoute.GetProperty("Match").GetProperty("Hosts")));

        Assert.True(routes.TryGetProperty("users-apisite-route", out var apisiteRoute), "users-apisite-route should exist.");
        Assert.Equal("users-cluster", apisiteRoute.GetProperty("ClusterId").GetString());
        Assert.Equal("/apisite/users/{**catch-all}", apisiteRoute.GetProperty("Match").GetProperty("Path").GetString());
        Assert.Contains("www.averia.lol", ReadStringArray(apisiteRoute.GetProperty("Match").GetProperty("Hosts")));

        Assert.True(clusters.TryGetProperty("users-cluster", out var usersCluster), "users-cluster should exist.");
        Assert.Equal(
            "http://127.0.0.1:5209",
            usersCluster.GetProperty("Destinations").GetProperty("primary").GetProperty("Address").GetString());
    }

    [Fact]
    public void ApiProxyConfig_DeclaresUsersRoutesAsInternalForwardedAuthTargets()
    {
        using var document = LoadApiProxyAppSettings();
        var root = document.RootElement;

        Assert.Contains("users.averia.lol", ReadStringArray(root.GetProperty("InternalServiceHosts")));

        var matchingRoute = root.GetProperty("InternalServiceRoutes")
            .EnumerateArray()
            .FirstOrDefault(route =>
                route.TryGetProperty("Hosts", out var hosts) &&
                ReadStringArray(hosts).Contains("www.averia.lol") &&
                route.TryGetProperty("PathPrefixes", out var prefixes) &&
                ReadStringArray(prefixes).Contains("/apisite/users/"));

        Assert.NotEqual(JsonValueKind.Undefined, matchingRoute.ValueKind);
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
