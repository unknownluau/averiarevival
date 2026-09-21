using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Roblox.ApiProxy.Configuration;
using Roblox.ApiProxy.Middleware;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Http;
using Yarp.ReverseProxy.Forwarder;

namespace Roblox.ApiProxy.Tests;

public class FrontendProxyMiddlewareTests
{
    [Fact]
    public async Task FrontendPath_WithoutSession_RedirectsToRoot()
    {
        var (context, forwarder) = await InvokeAsync();

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location.ToString());
        Assert.Equal(0, forwarder.ForwardCount);
    }

    [Theory]
    [InlineData(AccountStatus.Suppressed)]
    [InlineData(AccountStatus.Poisoned)]
    [InlineData(AccountStatus.Deleted)]
    public async Task FrontendPath_WithNotApprovedSession_RedirectsToNotApproved(AccountStatus accountStatus)
    {
        var (context, forwarder) = await InvokeAsync(CreateSession(accountStatus));

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/auth/notapproved", context.Response.Headers.Location.ToString());
        Assert.Equal(0, forwarder.ForwardCount);
    }

    [Fact]
    public async Task FrontendPath_WithOkSession_ForwardsToFrontend()
    {
        var (context, forwarder) = await InvokeAsync(CreateSession(AccountStatus.Ok));

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("forwarded", await ReadBodyAsync(context));
        Assert.Equal(1, forwarder.ForwardCount);
    }

    [Theory]
    [InlineData("/_next/static/chunks/main.js")]
    [InlineData("/period-styles/early-2018.example.css")]
    [InlineData("/theme-assets/source-sans-pro.example.woff2")]
    [InlineData("/js/bootstrap.min.css")]
    [InlineData("/js/3d/three-r137/three.js")]
    [InlineData("/js/3d/three-r137/OBJLoaderr.js")]
    [InlineData("/js/roblox/icons.css")]
    [InlineData("/img/generic_light_2025.svg")]
    public async Task FrontendPublicAsset_WithoutSession_ForwardsToFrontend(string path)
    {
        var (context, forwarder) = await InvokeAsync(path: path);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("forwarded", await ReadBodyAsync(context));
        Assert.Equal(1, forwarder.ForwardCount);
    }

    [Fact]
    public async Task BackendOwnedJsPath_WithoutSession_ContinuesToNextMiddleware()
    {
        var nextCalled = false;
        var (context, forwarder) = await InvokeAsync(
            path: "/js/legacy-bundle.js",
            next: _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, forwarder.ForwardCount);
        Assert.True(nextCalled);
    }

    private static async Task<(DefaultHttpContext Context, FakeForwarder Forwarder)> InvokeAsync(
        UserSession? session = null,
        string path = "/home",
        RequestDelegate? next = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("www.averia.lol");
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        context.SetRobloxRequestContext(new RobloxRequestContext
        {
            Session = session,
            IsAuthenticated = session != null,
        });

        var forwarder = new FakeForwarder();
        var middleware = new FrontendProxyMiddleware(
            next ?? (_ => Task.CompletedTask),
            forwarder,
            Options.Create(new FrontendProxyOptions
            {
                DestinationPrefix = "http://127.0.0.1:3000/",
                PublicHosts = new[] { "www.averia.lol" },
            }),
            NullLogger<FrontendProxyMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return (context, forwarder);
    }

    private static UserSession CreateSession(AccountStatus accountStatus)
    {
        return new UserSession(123, "FrontendUser", DateTime.UtcNow, accountStatus, 0, false, "session-id");
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private sealed class FakeForwarder : IHttpForwarder
    {
        public int ForwardCount { get; private set; }

        public ValueTask<ForwarderError> SendAsync(
            HttpContext context,
            string destinationPrefix,
            HttpMessageInvoker httpClient,
            ForwarderRequestConfig requestConfig,
            HttpTransformer transformer)
        {
            return SendAsync(context, destinationPrefix, httpClient, requestConfig, transformer, CancellationToken.None);
        }

        public async ValueTask<ForwarderError> SendAsync(
            HttpContext context,
            string destinationPrefix,
            HttpMessageInvoker httpClient,
            ForwarderRequestConfig requestConfig,
            HttpTransformer transformer,
            CancellationToken cancellationToken)
        {
            ForwardCount++;
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync("forwarded", cancellationToken);
            return ForwarderError.None;
        }
    }
}
