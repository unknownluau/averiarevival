using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Dto.Admin;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services.Admin.Controllers;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Services.Admin.Tests;

public sealed class FixBuggedRendersRouteTests : IDisposable
{
    private readonly TestServer _server;

    public FixBuggedRendersRouteTests()
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
                services.AddSingleton<IAdminStaffAuthorizationService, FakeStaffAuthorization>();
                services.AddSingleton<IAdminTwoFactorStore, FakeTwoFactorStore>();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-User", out var value) && long.TryParse(value, out var userId))
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
    public async Task FixBuggedRenders_Unauthenticated_ReturnsForbidden()
    {
        using var response = await _server.CreateClient().PostAsJsonAsync(
            "/v1/asset/fix-bugged-renders", new { ownership = "roblox" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FixBuggedRenders_InvalidOwnership_ReturnsValidationProblem()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/asset/fix-bugged-renders")
        {
            Content = JsonContent.Create(new { limit = 25, newestFirst = false, ownership = "groups" }),
        };
        request.Headers.Add("X-Test-User", "1");
        using var response = await _server.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Contains("ownership", problem.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("roblox")]
    [InlineData("user")]
    public void FixBuggedRenders_SupportedOwnershipValues_AreValid(string ownership)
    {
        var request = new FixBuggedRendersRequest { ownership = ownership };
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request, new System.ComponentModel.DataAnnotations.ValidationContext(request), validationResults, true));
    }

    [Fact]
    public void FixBuggedRenders_OmittedOwnership_DefaultsToRoblox()
    {
        Assert.Equal("roblox", new FixBuggedRendersRequest().ownership);
    }

    public void Dispose() => _server.Dispose();

    private sealed class FakeStaffAuthorization : IAdminStaffAuthorizationService
    {
        public bool IsOwner(long userId) => userId == 1;
        public Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId) =>
            Task.FromResult<IReadOnlyCollection<Access>>(new[] { Access.RequestAssetReRender });
        public Task<bool> IsStaffAsync(long userId) => Task.FromResult(true);
    }

    private sealed class FakeTwoFactorStore : IAdminTwoFactorStore
    {
        public Task<bool> IsVerifiedAsync(long userId, string sessionId) => Task.FromResult(true);
        public Task MarkVerifiedAsync(long userId, string sessionId) => Task.CompletedTask;
        public Task InvalidateAsync(long userId, string sessionId) => Task.CompletedTask;
    }
}
