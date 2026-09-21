using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Services;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;
using Roblox.Website.Controllers;

namespace Roblox.Web.Infrastructure.Tests;

public class SessionNegotiationRouteTests
{
    [Theory]
    [InlineData("GET", "/login/negotiate.ashx")]
    [InlineData("GET", "/login/negotiateasync.ashx")]
    [InlineData("POST", "/login/negotiate.ashx")]
    public async Task ValidTicket_IsExchangedOnce(string method, string path)
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
        {
            return;
        }

        await using var host = await CreateHostAsync();
        using var tickets = new SessionNegotiationTicketService();
        var ticket = await tickets.IssueAsync("signed-session-token", "ip");

        using var response = await host.Client.SendAsync(CreateRequest(method, $"{path}?suggest={ticket}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        Assert.Equal(2, cookies.Count);
        Assert.All(cookies, cookie => Assert.Contains("signed-session-token", cookie, StringComparison.Ordinal));
        Assert.All(cookies, cookie => Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase));
        Assert.All(cookies, cookie => Assert.DoesNotContain(ticket, cookie, StringComparison.Ordinal));

        using var replay = await host.Client.SendAsync(CreateRequest(method, $"{path}?suggest={ticket}"));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.False(replay.Headers.Contains("Set-Cookie"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?suggest=invalid")]
    public async Task InvalidTicket_ReturnsUnauthorizedWithoutCookies(string query)
    {
        if (await DockerInfrastructureFixture.CreateAsync() == null)
        {
            return;
        }

        await using var host = await CreateHostAsync();
        using var response = await host.Client.SendAsync(CreateRequest("GET", "/login/negotiate.ashx" + query));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static HttpRequestMessage CreateRequest(string method, string path)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.UserAgent.ParseAdd("Roblox/WinInet");
        return request;
    }

    private static async Task<RouteTestHost> CreateHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddOptions<RobloxWebInfrastructureOptions>();
        builder.Services.AddScoped(_ => new RobloxServiceAccessor());
        builder.Services.AddSingleton<IRobloxRequestContextAccessor, RobloxRequestContextAccessor>();
        builder.Services.AddSingleton<FileContentCache>();
        builder.Services.AddControllers().AddApplicationPart(typeof(BypassController).Assembly);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.SetRobloxRequestContext(new RobloxRequestContext
            {
                HashedIp = "ip",
                IsRobloxClient = true,
                UserAgent = "Roblox/WinInet",
            });
            await next();
        });
        app.MapControllers();
        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!
            .Addresses;
        var handler = new HttpClientHandler { UseCookies = false };
        var client = new HttpClient(handler) { BaseAddress = new Uri(addresses.Single()) };
        return new RouteTestHost(app, client);
    }

    private sealed class RouteTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
