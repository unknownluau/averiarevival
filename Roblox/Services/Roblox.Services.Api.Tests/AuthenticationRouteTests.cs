using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Routing;
using Roblox.Services.Api.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Tests;

public class AuthenticationRouteTests
{
    private static readonly IReadOnlyList<AuthenticationRouteCase> Routes = new List<AuthenticationRouteCase>
    {
        new("POST", "/v1/login", AllowsAnonymous: true, RequiresRobloxClient: true, RequiresSession: false),
        new("POST", "/v2/login", AllowsAnonymous: true, RequiresRobloxClient: true, RequiresSession: false),
        new("GET", "/sign-out/v1", AllowsAnonymous: false, RequiresRobloxClient: true, RequiresSession: true),
        new("POST", "/sign-out/v1", AllowsAnonymous: false, RequiresRobloxClient: true, RequiresSession: true),
    };

    [Fact]
    public void RouteMatrix_CoversEveryAuthenticationControllerRoute()
    {
        var declaredRoutes = typeof(AuthenticationController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .SelectMany(attribute => attribute.Template == null
                ? Enumerable.Empty<(string Method, string Path)>()
                : attribute.HttpMethods.Select(method => (Method: method.ToUpperInvariant(), Path: NormalizeRoute(attribute.Template))))
            .ToHashSet();

        var matrixRoutes = Routes
            .Select(route => (route.Method, route.Path))
            .ToHashSet();

        var missing = declaredRoutes.Except(matrixRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();
        var extra = matrixRoutes.Except(declaredRoutes).OrderBy(route => route.Method).ThenBy(route => route.Path).ToList();

        Assert.True(missing.Count == 0, "Missing Authentication route matrix entries: " + string.Join(", ", missing));
        Assert.True(extra.Count == 0, "Authentication route matrix contains entries not declared by controller: " + string.Join(", ", extra));
    }

    [Fact]
    public void AuthenticationRoutes_HaveExpectedMetadata()
    {
        foreach (var method in typeof(AuthenticationController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            var httpAttributes = method.GetCustomAttributes<HttpMethodAttribute>().ToArray();
            if (httpAttributes.Length == 0)
            {
                continue;
            }

            foreach (var httpAttribute in httpAttributes)
            {
                foreach (var httpMethod in httpAttribute.HttpMethods)
                {
                    var route = Routes.Single(route =>
                        route.Method == httpMethod.ToUpperInvariant() &&
                        route.Path == NormalizeRoute(httpAttribute.Template!));

                    Assert.Equal(route.AllowsAnonymous, method.GetCustomAttribute<AllowRobloxAnonymousAttribute>() != null);
                    Assert.Equal(route.RequiresRobloxClient, method.GetCustomAttribute<RequireRobloxClientAttribute>() != null);
                    Assert.Equal(route.RequiresSession, method.GetCustomAttribute<RequireRobloxSessionAttribute>() != null);
                }
            }

            Assert.Null(method.GetCustomAttribute<RequireRccRequestAttribute>());
        }
    }

    [Theory]
    [InlineData("/v1/login")]
    [InlineData("/v2/login")]
    public async Task LoginRoutes_RejectNonRobloxRequests(string path)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.PostAsync(path, JsonContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginV2_EmptyBody_ReturnsStableBadRequestError()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true);
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.PostAsync("/v2/login", new StringContent(string.Empty, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRobloxError(response, 8, "Empty request body.");
    }

    [Fact]
    public async Task LoginV2_MissingUsernameOrPassword_ReturnsStableBadRequestError()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true);
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.PostAsync("/v2/login", JsonContent(new { username = "", password = "" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRobloxError(response, 3, "Username and Password are required. Please try again.");
    }

    [Fact]
    public async Task LoginV1_WithValidCredentials_SetsSessionCookiesAndReturnsUserShape()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true, handleCookies: false);
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.Client.PostAsync("/v1/login", JsonContent(new
        {
            cvalue = user.Username,
            password = user.Password,
        }));

        await AssertStatusCode(response, HttpStatusCode.OK);
        AssertSessionCookies(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        var responseUser = root.GetProperty("user");
        Assert.Equal(user.UserId, responseUser.GetProperty("id").GetInt64());
        Assert.Equal(user.Username, responseUser.GetProperty("name").GetString());
        Assert.Equal(user.Username, responseUser.GetProperty("displayName").GetString());
        Assert.False(root.GetProperty("isBanned").GetBoolean());
    }

    [Fact]
    public async Task LoginV2_WithValidCredentials_SetsSessionCookiesAndReturnsLegacyShape()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true, handleCookies: false);
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var response = await fixture.Client.PostAsync("/v2/login", JsonContent(new
        {
            username = user.Username,
            password = user.Password,
        }));

        await AssertStatusCode(response, HttpStatusCode.OK);
        AssertSessionCookies(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        Assert.Equal(4, root.GetProperty("membershipType").GetInt32());
        Assert.Equal(user.Username, root.GetProperty("username").GetString());
        Assert.Equal(user.UserId, root.GetProperty("userId").GetInt64());
        Assert.Equal(user.UserId, root.GetProperty("id").GetInt64());
        Assert.Equal("US", root.GetProperty("countryCode").GetString());
        Assert.False(root.GetProperty("isBanned").GetBoolean());
        Assert.Equal(user.UserId, root.GetProperty("user").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task LoginV2_WithAndroidClientUserAgentAndDeviceHandle_SetsSessionCookies()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(handleCookies: false);
        if (fixture == null)
        {
            return;
        }

        fixture.Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (1980MB; 1080x2072; 440x440; 392x753; Google sdk_gphone_x86; 11) AppleWebKit/537.36 (KHTML, like Gecko) ROBLOX Android App 2.311.156028 Phone Hybrid() GooglePlayStore");

        var user = await fixture.CreateUserAsync();
        var response = await fixture.Client.PostAsync("/v2/login", JsonContent(new
        {
            deviceHandle = "17568685581919560531",
            username = user.Username,
            password = user.Password,
        }));

        await AssertStatusCode(response, HttpStatusCode.OK);
        AssertSessionCookies(response);
    }

    [Fact]
    public async Task LoginV2_WithAndroidClientFormPayloadAndDeviceHandle_SetsSessionCookies()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(handleCookies: false);
        if (fixture == null)
        {
            return;
        }

        fixture.Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (1980MB; 1080x2072; 440x440; 392x753; Google sdk_gphone_x86; 11) AppleWebKit/537.36 (KHTML, like Gecko) ROBLOX Android App 2.311.156028 Phone Hybrid() GooglePlayStore");

        var user = await fixture.CreateUserAsync();
        var response = await fixture.Client.PostAsync("/v2/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = user.Username,
            ["password"] = user.Password,
            ["deviceHandle"] = "02504034d7f9,d85ed30ce3f7,0a0027000008,00155dc804c3",
        }));

        await AssertStatusCode(response, HttpStatusCode.OK);
        AssertSessionCookies(response);
    }

    [Fact]
    public async Task LoginV2_WithTotpEnabled_ReturnsTwoStepRequiredWithoutSessionCookies()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true, handleCookies: false);
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync(totpEnabled: true);
        var response = await fixture.Client.PostAsync("/v2/login", JsonContent(new
        {
            username = user.Username,
            password = user.Password,
        }));

        await AssertStatusCode(response, HttpStatusCode.OK);
        AssertNoSessionCookies(response);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        Assert.Equal("TwoStepVerificationRequired", root.GetProperty("message").GetString());
        Assert.Equal("Email", root.GetProperty("mediaType").GetString());
        Assert.Equal(6, root.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("tl").GetString()));
        Assert.Equal(
            root.GetProperty("tl").GetString(),
            root.GetProperty("twoStepVerificationData").GetProperty("ticket").GetString());
        Assert.Equal(user.UserId, root.GetProperty("user").GetProperty("id").GetInt64());
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task SignOutV1_RejectsUnauthenticatedRobloxClientRequests(string method)
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true);
        if (fixture == null)
        {
            return;
        }

        var response = await fixture.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), "/sign-out/v1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignOutV1_WithAuthenticatedSessionDeletesSessionCookies()
    {
        await using var fixture = await ApiRouteTestFixture.CreateAsync(robloxClient: true);
        if (fixture == null)
        {
            return;
        }

        var user = await fixture.CreateUserAsync();
        var loginResponse = await fixture.Client.PostAsync("/v2/login", JsonContent(new
        {
            username = user.Username,
            password = user.Password,
        }));
        await AssertStatusCode(loginResponse, HttpStatusCode.OK);

        var signOutResponse = await fixture.Client.PostAsync("/sign-out/v1", null);

        await AssertStatusCode(signOutResponse, HttpStatusCode.OK);
        AssertDeletedSessionCookies(signOutResponse);
    }

    private static StringContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
    }

    private static async Task AssertRobloxError(HttpResponseMessage response, int expectedCode, string expectedMessage)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var error = json.RootElement.GetProperty("errors")[0];
        Assert.Equal(expectedCode, error.GetProperty("code").GetInt32());
        Assert.Equal(expectedMessage, error.GetProperty("message").GetString());
    }

    private static async Task AssertStatusCode(HttpResponseMessage response, HttpStatusCode expectedStatusCode)
    {
        Assert.True(
            response.StatusCode == expectedStatusCode,
            $"Expected {(int)expectedStatusCode} {expectedStatusCode}, got {(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static string NormalizeRoute(string route)
    {
        return "/" + route.Trim('/');
    }

    private static void AssertSessionCookies(HttpResponseMessage response)
    {
        var cookies = GetSetCookies(response);
        Assert.Contains(cookies, cookie => cookie.StartsWith(".ROBLOSECURITY=", StringComparison.Ordinal));
        Assert.Contains(cookies, cookie => cookie.StartsWith(".AVERISECURITY=", StringComparison.Ordinal));
    }

    private static void AssertNoSessionCookies(HttpResponseMessage response)
    {
        var cookies = GetSetCookies(response);
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(".ROBLOSECURITY=", StringComparison.Ordinal));
        Assert.DoesNotContain(cookies, cookie => cookie.StartsWith(".AVERISECURITY=", StringComparison.Ordinal));
    }

    private static void AssertDeletedSessionCookies(HttpResponseMessage response)
    {
        var cookies = GetSetCookies(response);
        Assert.Contains(cookies, cookie => IsDeletedCookie(cookie, ".ROBLOSECURITY"));
        Assert.Contains(cookies, cookie => IsDeletedCookie(cookie, ".AVERISECURITY"));
        // Assert.Contains(cookies, cookie => IsDeletedCookie(cookie, ".PEKORASECURITY"));
    }

    private static bool IsDeletedCookie(string cookie, string name)
    {
        return cookie.StartsWith(name + "=;", StringComparison.Ordinal) &&
               cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetSetCookies(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToList()
            : Array.Empty<string>();
    }

    public sealed record AuthenticationRouteCase(
        string Method,
        string Path,
        bool AllowsAnonymous,
        bool RequiresRobloxClient,
        bool RequiresSession)
    {
        public override string ToString()
        {
            return $"{Method} {Path}";
        }
    }

    [Fact]
    public async Task DockerInfrastructure_IsAvailableWhenRequired()
    {
        if (!ApiRouteTestFixture.DockerTestsRequired)
        {
            return;
        }

        Assert.True(await ApiRouteTestFixture.IsInfrastructureAvailableAsync());
    }
}
