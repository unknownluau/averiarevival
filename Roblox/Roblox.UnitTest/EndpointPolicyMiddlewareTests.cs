using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Web.Infrastructure.Middleware;
using Roblox.Website.Middleware;

namespace Roblox.UnitTest;

public class EndpointPolicyMiddlewareTests
{
    private const string ProxyAuthorization = "proxy-secret";
    private const string RccAuthorization = "rcc-secret";

    static EndpointPolicyMiddlewareTests()
    {
        try
        {
            CsrfMiddleware.Configure("csrf-test-secret");
        }
        catch (Exception)
        {
            // The middleware uses process-wide configuration; other tests or app setup may configure it first.
        }
    }

    [Fact]
    public void EndpointMetadata_DefaultsToBrowserFacingWithoutExplicitRequirements()
    {
        var endpoint = CreateEndpoint();

        Assert.False(endpoint.HasExplicitRobloxRequestRequirement());
        Assert.False(endpoint.IsInternalServiceOnly());
        Assert.False(endpoint.RequiresRobloxSession());
        Assert.False(endpoint.RequiresRobloxCsrf());
        Assert.False(endpoint.RequiresRccRequest());
        Assert.False(endpoint.RequiresRobloxClient());
    }

    [Fact]
    public void EndpointMetadata_DetectsExplicitRequirements()
    {
        var endpoint = CreateEndpoint(
            new InternalServiceOnlyAttribute(),
            new RequireRobloxSessionAttribute(),
            new RequireRobloxCsrfAttribute(),
            new RequireRccRequestAttribute(),
            new RequireRobloxClientAttribute());

        Assert.True(endpoint.HasExplicitRobloxRequestRequirement());
        Assert.True(endpoint.IsInternalServiceOnly());
        Assert.True(endpoint.RequiresRobloxSession());
        Assert.True(endpoint.RequiresRobloxCsrf());
        Assert.True(endpoint.RequiresRccRequest());
        Assert.True(endpoint.RequiresRobloxClient());
    }

    [Fact]
    public async Task ProxyForwardedAuth_AllowsDefaultEndpointWithoutAuthorizationHeader()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(CreateEndpoint());

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_RequiresInternalServiceAuthorizationWhenMarked()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(CreateEndpoint(new InternalServiceOnlyAttribute()));

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_AllowsInternalServiceEndpointWithValidAuthorization()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(
            CreateEndpoint(new InternalServiceOnlyAttribute()),
            ctx => ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = ProxyAuthorization);

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_RequiresSessionWhenMarked()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(CreateEndpoint(new RequireRobloxSessionAttribute()));

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_AllowsSessionEndpointWithForwardedSession()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(
            CreateEndpoint(new RequireRobloxSessionAttribute()),
            AddForwardedSessionHeaders);

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_RequiresRccRequestWhenMarked()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(CreateEndpoint(new RequireRccRequestAttribute()));

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_AllowsRccEndpointWithAccessKey()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(
            CreateEndpoint(new RequireRccRequestAttribute()),
            ctx => ctx.Request.Headers["accesskey"] = RccAuthorization);

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_RequiresRobloxClientWhenMarked()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(
            CreateEndpoint(new RequireRobloxClientAttribute()),
            ctx => ctx.Request.Headers.UserAgent = "Mozilla/5.0");

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_AllowsRobloxClientEndpointWithRobloxUserAgent()
    {
        var (context, nextCalled) = await InvokeProxyMiddlewareAsync(
            CreateEndpoint(new RequireRobloxClientAttribute()),
            ctx => ctx.Request.Headers.UserAgent = "Roblox/WinInet");

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProxyForwardedAuth_RequiresAllMarkedRequirements()
    {
        var endpoint = CreateEndpoint(
            new InternalServiceOnlyAttribute(),
            new RequireRobloxSessionAttribute(),
            new RequireRccRequestAttribute(),
            new RequireRobloxClientAttribute());

        var (missingSessionContext, missingSessionNextCalled) = await InvokeProxyMiddlewareAsync(endpoint, ctx =>
        {
            ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = ProxyAuthorization;
            ctx.Request.Headers["accesskey"] = RccAuthorization;
            ctx.Request.Headers.UserAgent = "Roblox/WinInet";
        });

        Assert.False(missingSessionNextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, missingSessionContext.Response.StatusCode);

        var (completeContext, completeNextCalled) = await InvokeProxyMiddlewareAsync(endpoint, ctx =>
        {
            ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = ProxyAuthorization;
            ctx.Request.Headers["accesskey"] = RccAuthorization;
            ctx.Request.Headers.UserAgent = "Roblox/WinInet";
            AddForwardedSessionHeaders(ctx);
        });

        Assert.True(completeNextCalled());
        Assert.Equal(StatusCodes.Status200OK, completeContext.Response.StatusCode);
    }

    [Fact]
    public async Task ApplicationGuard_AllowsDefaultEndpointWithoutAuthorizationHeader()
    {
        var (context, nextCalled) = await InvokeApplicationGuardAsync(CreateEndpoint());

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApplicationGuard_RequiresInternalServiceAuthorizationWhenMarked()
    {
        var (context, nextCalled) = await InvokeApplicationGuardAsync(CreateEndpoint(new InternalServiceOnlyAttribute()));

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task CsrfMiddleware_AllowsUnmarkedUnsafeMethods()
    {
        var (context, nextCalled) = await InvokeCsrfMiddlewareAsync(CreateEndpoint(), "POST");

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task CsrfMiddleware_RequiresTokenForMarkedUnsafeMethods()
    {
        var (context, nextCalled) = await InvokeCsrfMiddlewareAsync(
            CreateEndpoint(new RequireRobloxCsrfAttribute()),
            "POST");

        Assert.False(nextCalled());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers["x-csrf-token"].ToString()));
    }

    [Fact]
    public async Task CsrfMiddleware_AllowsMarkedUnsafeMethodsWithValidToken()
    {
        const string csrf = "csrf-value";
        var token = CsrfMiddleware.CreateJwt(new CsrfJwtEntry
        {
            csrf = csrf,
            createdAt = DateTime.UtcNow,
        });

        var (context, nextCalled) = await InvokeCsrfMiddlewareAsync(
            CreateEndpoint(new RequireRobloxCsrfAttribute()),
            "POST",
            ctx =>
            {
                ctx.Request.Headers.Cookie = $"{CsrfMiddleware.CookieName}={token}";
                ctx.Request.Headers["x-csrf-token"] = csrf;
            });

        Assert.True(nextCalled());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static Endpoint CreateEndpoint(params object[] metadata)
    {
        return new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");
    }

    private static async Task<(DefaultHttpContext Context, Func<bool> NextCalled)> InvokeProxyMiddlewareAsync(
        Endpoint endpoint,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = CreateHttpContext(endpoint);
        configure?.Invoke(context);

        var options = Options.Create(new RobloxWebInfrastructureOptions
        {
            Authorization = ProxyAuthorization,
            RccAuthorization = RccAuthorization,
        });
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = context,
        };
        var requestContextAccessor = new RobloxRequestContextAccessor(httpContextAccessor, options);
        var middleware = new ProxyForwardedAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, options);

        await middleware.InvokeAsync(context, requestContextAccessor);

        return (context, () => nextCalled);
    }

    private static async Task<(DefaultHttpContext Context, Func<bool> NextCalled)> InvokeApplicationGuardAsync(
        Endpoint endpoint,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = CreateHttpContext(endpoint);
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.SetRobloxRequestContext(RobloxRequestContextFactory.CreateAnonymous(context, RccAuthorization));
        configure?.Invoke(context);
        ApplicationGuardMiddleware.Configure(ProxyAuthorization);

        var middleware = new ApplicationGuardMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        return (context, () => nextCalled);
    }

    private static async Task<(DefaultHttpContext Context, Func<bool> NextCalled)> InvokeCsrfMiddlewareAsync(
        Endpoint endpoint,
        string method,
        Action<DefaultHttpContext>? configure = null)
    {
        var nextCalled = false;
        var context = CreateHttpContext(endpoint);
        context.Request.Method = method;
        configure?.Invoke(context);

        var middleware = new CsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        return (context, () => nextCalled);
    }

    private static DefaultHttpContext CreateHttpContext(Endpoint endpoint)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(endpoint);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static void AddForwardedSessionHeaders(DefaultHttpContext context)
    {
        context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = "123";
        context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName] = "TestUser";
        context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName] = "session-id";
        context.Request.Headers[RobloxWebContextConstants.AccountStatusHeaderName] = AccountStatus.Ok.ToString();
    }

}
