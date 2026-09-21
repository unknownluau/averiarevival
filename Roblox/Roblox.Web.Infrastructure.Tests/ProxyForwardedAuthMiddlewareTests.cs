using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Web.Infrastructure.Tests;

public class ProxyForwardedAuthMiddlewareTests
{
    [Fact]
    public async Task DefaultEndpoint_AllowsRequestAndStoresContext()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            InfrastructureTestHelpers.Endpoint());

        Assert.True(nextCalled);
        Assert.NotNull(context.GetRobloxRequestContext());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InternalServiceEndpoint_RejectsMissingAuthorizationWithRobloxErrorShape()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            InfrastructureTestHelpers.Endpoint(new InternalServiceOnlyAttribute()));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        await AssertUnauthorizedErrorShape(context);
    }

    [Fact]
    public async Task InternalServiceEndpoint_AllowsValidAuthorization()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            InfrastructureTestHelpers.Endpoint(new InternalServiceOnlyAttribute()),
            ctx => ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = TestConstants.ProxyAuthorization);

        Assert.True(nextCalled);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task SessionEndpoint_RejectsAnonymousAndAllowsForwardedSession()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(new RequireRobloxSessionAttribute());

        var (anonymousContext, anonymousNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(endpoint);
        var (sessionContext, sessionNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            endpoint,
            ctx => InfrastructureTestHelpers.AddForwardedSessionHeaders(ctx));

        Assert.False(anonymousNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, anonymousContext.Response.StatusCode);
        Assert.True(sessionNextCalled);
        Assert.True(sessionContext.GetRobloxRequestContext()!.IsAuthenticated);
    }

    [Fact]
    public async Task RccEndpoint_RejectsMissingAccessKeyAndAllowsValidAccessKey()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(new RequireRccRequestAttribute());

        var (anonymousContext, anonymousNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(endpoint);
        var (rccContext, rccNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            endpoint,
            ctx => ctx.Request.Headers["accesskey"] = TestConstants.RccAuthorization);

        Assert.False(anonymousNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, anonymousContext.Response.StatusCode);
        Assert.True(rccNextCalled);
        Assert.True(rccContext.GetRobloxRequestContext()!.IsRcc);
    }

    [Fact]
    public async Task RobloxClientEndpoint_RejectsBrowserAndAllowsRobloxUserAgent()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(new RequireRobloxClientAttribute());

        var (browserContext, browserNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            endpoint,
            ctx => ctx.Request.Headers.UserAgent = "Mozilla/5.0");
        var (robloxContext, robloxNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(
            endpoint,
            ctx => ctx.Request.Headers.UserAgent = "Roblox/WinInet");

        Assert.False(browserNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, browserContext.Response.StatusCode);
        Assert.True(robloxNextCalled);
        Assert.True(robloxContext.GetRobloxRequestContext()!.IsRobloxClient);
    }

    [Fact]
    public async Task MultipleRequirements_AreAllRequired()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(
            new InternalServiceOnlyAttribute(),
            new RequireRobloxSessionAttribute(),
            new RequireRccRequestAttribute(),
            new RequireRobloxClientAttribute());

        var (partialContext, partialNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(endpoint, ctx =>
        {
            ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = TestConstants.ProxyAuthorization;
            ctx.Request.Headers["accesskey"] = TestConstants.RccAuthorization;
            ctx.Request.Headers.UserAgent = "Roblox/WinInet";
        });

        var (completeContext, completeNextCalled) = await InfrastructureTestHelpers.InvokeProxyForwardedAuthAsync(endpoint, ctx =>
        {
            ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = TestConstants.ProxyAuthorization;
            ctx.Request.Headers["accesskey"] = TestConstants.RccAuthorization;
            ctx.Request.Headers.UserAgent = "Roblox/WinInet";
            InfrastructureTestHelpers.AddForwardedSessionHeaders(ctx);
        });

        Assert.False(partialNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, partialContext.Response.StatusCode);
        Assert.True(completeNextCalled);
        Assert.Equal(StatusCodes.Status200OK, completeContext.Response.StatusCode);
    }

    private static async Task AssertUnauthorizedErrorShape(DefaultHttpContext context)
    {
        using var json = await InfrastructureTestHelpers.ReadJsonAsync(context);
        Assert.True(json.RootElement.TryGetProperty("errors", out var errors));
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal("Unauthorized (PRX)", errors[0].GetProperty("message").GetString());
    }
}
