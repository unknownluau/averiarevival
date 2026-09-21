using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roblox.ApiProxy.Configuration;
using Roblox.ApiProxy.Middleware;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Admin;

namespace Roblox.ApiProxy.Tests;

public class AdminFrontendRouteTests : IDisposable
{
    private readonly string _adminRoot;
    private readonly FakeAdminSessionResolver _sessionResolver = new();
    private readonly FakeAdminStaffAuthorizationService _staffAuthorization = new();
    private readonly FakeAdminTwoFactorStore _twoFactorStore = new();
    private readonly TestServer _server;

    public AdminFrontendRouteTests()
    {
        _adminRoot = Path.Combine(Path.GetTempPath(), "averia-admin-proxy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_adminRoot, "build"));
        File.WriteAllText(Path.Combine(_adminRoot, "index.html"), "<main>admin shell</main>");
        File.WriteAllText(Path.Combine(_adminRoot, "favicon.png"), "png");
        File.WriteAllText(Path.Combine(_adminRoot, "build", "bundle.js"), "console.log('admin');");
        File.WriteAllText(Path.Combine(_adminRoot, "build", "bundle.css"), "body { color: black; }");

        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<AdminFrontendOptions>(options =>
                {
                    options.RootDirectory = _adminRoot;
                });
                services.Configure<AdminApiOptions>(options =>
                {
                    options.PublicBaseUrl = "https://admin.averia.lol/v1/";
                    options.CorsAllowedOrigins = new[] { "https://www.averia.lol" };
                });
                services.AddLogging();
                services.AddSingleton<IAdminSessionResolver>(_sessionResolver);
                services.AddSingleton<IAdminStaffAuthorizationService>(_staffAuthorization);
                services.AddSingleton<IAdminTwoFactorStore>(_twoFactorStore);
            })
            .Configure(app =>
            {
                app.UseMiddleware<AdminFrontendMiddleware>();
            }));
    }

    [Fact]
    public async Task AdminRoot_Unauthenticated_RedirectsHome()
    {
        var response = await CreateClient().GetAsync("/admin/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/home", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminPath_UnverifiedStaff_RedirectsToApiTwoFactorPromptWithReturnUrl()
    {
        _sessionResolver.Session = CreateSession();
        _staffAuthorization.IsStaff = true;
        _twoFactorStore.IsVerified = false;

        var response = await CreateClient().GetAsync("/admin/users?tab=staff");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://admin.averia.lol/v1/2fa?returnUrl=%2Fadmin%2Fusers%3Ftab%3Dstaff",
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminPath_VerifiedStaff_ServesSpaIndex()
    {
        _sessionResolver.Session = CreateSession();
        _staffAuthorization.IsStaff = true;
        _twoFactorStore.IsVerified = true;

        var response = await CreateClient().GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("admin shell", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/admin/build-redirect/bundle.js", "/bundle.js")]
    [InlineData("/admin/build-redirect/bundle.css", "/bundle.css")]
    public async Task AdminBundleRedirects_ReturnCacheBustedBundlePath(string path, string expectedSuffix)
    {
        _sessionResolver.Session = CreateSession();
        _staffAuthorization.IsStaff = true;
        _twoFactorStore.IsVerified = true;

        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.StartsWith("/admin/build/", location, StringComparison.Ordinal);
        Assert.EndsWith(expectedSuffix, location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin/build/cache-key/bundle.js", "application/javascript", "console.log")]
    [InlineData("/admin/build/cache-key/bundle.css", "text/css", "body")]
    public async Task AdminVersionedBundles_ReturnExpectedAsset(string path, string contentType, string expectedBody)
    {
        _sessionResolver.Session = CreateSession();
        _staffAuthorization.IsStaff = true;
        _twoFactorStore.IsVerified = true;

        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(expectedBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownAdminBuildAsset_ReturnsNotFound()
    {
        _sessionResolver.Session = CreateSession();
        _staffAuthorization.IsStaff = true;
        _twoFactorStore.IsVerified = true;

        var response = await CreateClient().GetAsync("/admin/build/cache-key/missing.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _server.Dispose();
        if (Directory.Exists(_adminRoot))
        {
            Directory.Delete(_adminRoot, recursive: true);
        }
    }

    private HttpClient CreateClient()
    {
        var client = _server.CreateClient();
        client.DefaultRequestHeaders.Host = "www.averia.lol";
        return client;
    }

    private static UserSession CreateSession()
    {
        return new UserSession(1, "Admin", DateTime.UtcNow, AccountStatus.Ok, 0, false, "session-id");
    }

    private sealed class FakeAdminSessionResolver : IAdminSessionResolver
    {
        public UserSession? Session { get; set; }

        public Task<UserSession?> TryResolveAsync(HttpContext context)
        {
            return Task.FromResult(Session);
        }
    }

    private sealed class FakeAdminStaffAuthorizationService : IAdminStaffAuthorizationService
    {
        public bool IsStaff { get; set; }

        public bool IsOwner(long userId)
        {
            return IsStaff;
        }

        public Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId)
        {
            IReadOnlyCollection<Access> permissions = IsStaff ? new[] { Access.GetStats } : Array.Empty<Access>();
            return Task.FromResult(permissions);
        }

        public Task<bool> IsStaffAsync(long userId)
        {
            return Task.FromResult(IsStaff);
        }
    }

    private sealed class FakeAdminTwoFactorStore : IAdminTwoFactorStore
    {
        public bool IsVerified { get; set; }

        public Task<bool> IsVerifiedAsync(long userId, string sessionId)
        {
            return Task.FromResult(IsVerified);
        }

        public Task MarkVerifiedAsync(long userId, string sessionId)
        {
            IsVerified = true;
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(long userId, string sessionId)
        {
            IsVerified = false;
            return Task.CompletedTask;
        }
    }
}
