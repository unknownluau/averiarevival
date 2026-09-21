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
using Roblox.Services.Admin.Telemetry;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Services.Admin.Tests;

public sealed class TelemetryRouteTests : IDisposable
{
    private readonly TestServer _server;

    public TelemetryRouteTests()
    {
        _server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers().AddApplicationPart(typeof(TelemetryController).Assembly)
                    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = null);
                services.AddSingleton<ITelemetryQueryService, FakeTelemetryQueryService>();
                services.AddSingleton<IAdminStaffAuthorizationService, FakeStaffAuthorization>();
                services.AddSingleton<IAdminTwoFactorStore, FakeTwoFactorStore>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-User", out var userValue) && long.TryParse(userValue, out var userId))
                    {
                        context.SetRobloxRequestContext(new RobloxRequestContext
                        {
                            IsAuthenticated = true,
                            IsTrustedInternalRequest = true,
                            Session = new UserSession(userId, "tester", DateTime.UtcNow, AccountStatus.Ok, 1, false, "session"),
                        });
                    }
                    await next();
                });
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }));
    }

    [Fact]
    public async Task Dashboard_Unauthenticated_ReturnsForbidden()
    {
        using var response = await _server.CreateClient().GetAsync("/v1/telemetry/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithoutPermission_ReturnsForbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/telemetry/dashboard");
        request.Headers.Add("X-Test-User", "2");
        using var response = await _server.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Dashboard_WithPermission_BindsQueryAndReturnsStableShape()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/telemetry/dashboard?range=1h&service=Roblox.Website");
        request.Headers.Add("X-Test-User", "1");
        using var response = await _server.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.Contains("GeneratedAt", json.Keys);
        Assert.Contains("Summary", json.Keys);
        Assert.Contains("Charts", json.Keys);
        Assert.Contains("RenderPool", json.Keys);
    }

    public void Dispose() => _server.Dispose();

    private sealed class FakeTelemetryQueryService : ITelemetryQueryService
    {
        public Task<TelemetryDashboardResponse> GetDashboardAsync(string range, string service, CancellationToken cancellationToken) =>
            Task.FromResult(new TelemetryDashboardResponse(DateTime.UtcNow, range, 15, service, new[] { "Roblox.Website" },
                new TelemetrySummary(1, 0, 10, 5, 90, 1, 100), Array.Empty<TelemetryChart>(),
                new RenderPoolSnapshot(3, 2, 1, 0, 0, 0, 3, 10, 0, 1, 0, 100, 110, true)));
    }

    private sealed class FakeStaffAuthorization : IAdminStaffAuthorizationService
    {
        public bool IsOwner(long userId) => userId == 1;
        public Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId) =>
            Task.FromResult<IReadOnlyCollection<Access>>(userId == 1 ? new[] { Access.ViewTelemetry } : Array.Empty<Access>());
        public Task<bool> IsStaffAsync(long userId) => Task.FromResult(userId is 1 or 2);
    }

    private sealed class FakeTwoFactorStore : IAdminTwoFactorStore
    {
        public Task<bool> IsVerifiedAsync(long userId, string sessionId) => Task.FromResult(true);
        public Task MarkVerifiedAsync(long userId, string sessionId) => Task.CompletedTask;
        public Task InvalidateAsync(long userId, string sessionId) => Task.CompletedTask;
    }
}
