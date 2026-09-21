using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Web.Infrastructure.Tests;

public class ApplicationGuardMiddlewareTests
{
    [Fact]
    public async Task AllowsDefaultEndpointWithoutAuthorization()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(
            InfrastructureTestHelpers.Endpoint());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task EnforcesExplicitInternalServiceRequirement()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(new InternalServiceOnlyAttribute());

        var (anonymousContext, anonymousNextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(endpoint);
        var (authorizedContext, authorizedNextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(
            endpoint,
            ctx => ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = TestConstants.ProxyAuthorization);

        Assert.False(anonymousNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, anonymousContext.Response.StatusCode);
        Assert.True(authorizedNextCalled);
        Assert.Equal(StatusCodes.Status200OK, authorizedContext.Response.StatusCode);
    }

    [Fact]
    public async Task EnforcesSessionRccAndRobloxClientRequirements()
    {
        var endpoint = InfrastructureTestHelpers.Endpoint(
            new RequireRobloxSessionAttribute(),
            new RequireRccRequestAttribute(),
            new RequireRobloxClientAttribute());

        var (missingContext, missingNextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(endpoint);
        var (completeContext, completeNextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(endpoint, ctx =>
        {
            var session = InfrastructureTestHelpers.CreateSession();
            ctx.SetRobloxRequestContext(new RobloxRequestContext
            {
                Session = session,
                IsAuthenticated = true,
                IsRcc = true,
                IsRobloxClient = true,
                UserAgent = "Roblox/WinInet",
            });
        });

        Assert.False(missingNextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, missingContext.Response.StatusCode);
        Assert.True(completeNextCalled);
        Assert.Equal(StatusCodes.Status200OK, completeContext.Response.StatusCode);
    }

    [Fact]
    public async Task BlocksCrawlerUserAgentsBeforeEndpointAuthorization()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(
            InfrastructureTestHelpers.Endpoint(),
            ctx => ctx.Request.Headers.UserAgent = "Googlebot/2.1");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/auth/captcha", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task RobotsTxtBypassesUserAgentBlockAndReturnsText()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApplicationGuardAsync(
            InfrastructureTestHelpers.Endpoint(new InternalServiceOnlyAttribute()),
            ctx =>
            {
                ctx.Request.Path = "/robots.txt";
                ctx.Request.Headers.UserAgent = "Googlebot/2.1";
            });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("user-agent: *", InfrastructureTestHelpers.ReadBody(context));
    }
}
