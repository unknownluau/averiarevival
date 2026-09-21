using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services.Admin.Controllers;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Services.Admin.Tests;

public sealed class AdminIdentityRouteTests : IDisposable
{
    private readonly TestServer _server;

    public AdminIdentityRouteTests()
    {
        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddHttpContextAccessor();
                services.AddOptions<RobloxWebInfrastructureOptions>();
                services.AddControllers().AddApplicationPart(typeof(AdminController).Assembly);
                services.AddScoped<RobloxServiceAccessor>();
                services.AddSingleton<IRobloxRequestContextAccessor, RobloxRequestContextAccessor>();
                services.AddSingleton<FileContentCache>();
                services.AddSingleton<IAdminStaffAuthorizationService, OwnerStaffAuthorization>();
                services.AddSingleton<IAdminTwoFactorStore, VerifiedTwoFactorStore>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.ContainsKey("X-Test-Owner"))
                    {
                        context.SetRobloxRequestContext(new RobloxRequestContext
                        {
                            IsAuthenticated = true,
                            IsTrustedInternalRequest = true,
                            Session = new UserSession(1, "owner", DateTime.UtcNow, AccountStatus.Ok, 1, false, "session"),
                        });
                    }
                    await next();
                });
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
    }

    [Theory]
    [InlineData("/v1/user/search-by-mac?macAddress=AA:BB:CC:DD:EE:FF")]
    [InlineData("/v1/user/search-by-ip?ipHash=test-hash")]
    [InlineData("/v1/user/alt-accounts?userId=1")]
    [InlineData("/v1/ip-ban/status?ipHash=test-hash")]
    public async Task IdentityLookupRoutes_Unauthenticated_ReturnForbidden(string path)
    {
        using var response = await _server.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/user/search-by-mac")]
    [InlineData("/v1/user/search-by-ip")]
    [InlineData("/v1/user/alt-accounts")]
    [InlineData("/v1/ip-ban/status")]
    public async Task IdentityLookupRoutes_MissingRequiredQuery_ReturnValidationProblem(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-Owner", "1");
        using var response = await _server.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IpBan_InvalidJsonShape_ReturnsValidationProblem()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/ip-ban")
        {
            Content = JsonContent.Create(new { ipHash = "hash", internalReason = "x" }),
        };
        request.Headers.Add("X-Test-Owner", "1");
        using var response = await _server.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose() => _server.Dispose();

    private sealed class OwnerStaffAuthorization : IAdminStaffAuthorizationService
    {
        public bool IsOwner(long userId) => userId == 1;
        public Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId) =>
            Task.FromResult<IReadOnlyCollection<Access>>(new[] { Access.ViewMacAddresses, Access.BanUser });
        public Task<bool> IsStaffAsync(long userId) => Task.FromResult(true);
    }

    private sealed class VerifiedTwoFactorStore : IAdminTwoFactorStore
    {
        public Task<bool> IsVerifiedAsync(long userId, string sessionId) => Task.FromResult(true);
        public Task MarkVerifiedAsync(long userId, string sessionId) => Task.CompletedTask;
        public Task InvalidateAsync(long userId, string sessionId) => Task.CompletedTask;
    }
}
