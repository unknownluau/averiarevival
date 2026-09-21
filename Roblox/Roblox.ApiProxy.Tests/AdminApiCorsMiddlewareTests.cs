using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roblox.ApiProxy.Configuration;
using Roblox.ApiProxy.Middleware;

namespace Roblox.ApiProxy.Tests;

public class AdminApiCorsMiddlewareTests
{
    [Fact]
    public async Task AdminHost_Preflight_AddsCredentialedCorsHeaders()
    {
        using var server = CreateServer();
        var request = new HttpRequestMessage(HttpMethod.Options, "/v1/users");
        request.Headers.Host = "admin.averia.lol";
        request.Headers.Add("Origin", "https://www.averia.lol");

        var response = await server.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://www.averia.lol", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        Assert.Contains("x-csrf-token", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.Contains("x-csrf-token", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
        Assert.False(string.IsNullOrWhiteSpace(response.Headers.GetValues("x-csrf-token").Single()));
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("rbxcsrf4="));
    }

    [Fact]
    public async Task AdminHost_NonV1Path_ReturnsNotFoundBeforeFallback()
    {
        using var server = CreateServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Host = "admin.averia.lol";

        var response = await client.GetAsync("/not-v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NonAdminHost_ContinuesToNextMiddleware()
    {
        using var server = CreateServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Host = "www.averia.lol";

        var response = await client.GetAsync("/not-v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("next", await response.Content.ReadAsStringAsync());
    }

    private static TestServer CreateServer()
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<AdminApiOptions>(options =>
                {
                    options.CorsAllowedOrigins = new[] { "https://www.averia.lol" };
                });
            })
            .Configure(app =>
            {
                app.UseMiddleware<AdminApiCorsMiddleware>();
                app.Run(async context => await context.Response.WriteAsync("next"));
            }));
    }
}
