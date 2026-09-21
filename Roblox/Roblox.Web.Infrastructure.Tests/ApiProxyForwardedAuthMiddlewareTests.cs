using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class ApiProxyForwardedAuthMiddlewareTests
{
    [Fact]
    public async Task DoesNotDecorateUnmatchedHostOrRoute()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(options =>
        {
            options.InternalServiceHosts.Add("internal.test.local");
            options.InternalServiceRoutes.Add(new RobloxInternalServiceRoute
            {
                Hosts = new List<string> { "api.test.local" },
                PathPrefixes = new List<string> { "/internal" },
            });
        });

        Assert.True(nextCalled);
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.ProxyAuthorizationHeaderName));
        Assert.NotNull(context.GetRobloxRequestContext());
        Assert.False(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task DecoratesConfiguredHostWithProxyHeadersAndTrustedContext()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("WWW.TEST.LOCAL"),
            ctx =>
            {
                ctx.Request.Headers.UserAgent = "Roblox/WinInet";
                ctx.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "client-hash";
                ctx.Request.Headers[RobloxWebContextConstants.GameIdHeaderName] = "game-1";
                ctx.Request.Headers[RobloxWebContextConstants.PlaceIdHeaderName] = "999";
            });

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.Equal("client-hash", context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName]);
        Assert.Equal("Roblox/WinInet", context.Request.Headers[RobloxWebContextConstants.UserAgentHeaderName]);
        Assert.Equal("game-1", context.Request.Headers[RobloxWebContextConstants.GameIdHeaderName]);
        Assert.Equal("999", context.Request.Headers[RobloxWebContextConstants.PlaceIdHeaderName]);
        Assert.Equal("roblox", context.Request.Headers[RobloxWebContextConstants.AuthTypeHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task DecoratesConfiguredWildcardHostWithProxyHeaders()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("*.api.averia.lol"),
            ctx => ctx.Request.Host = new HostString("gameinstances.api.averia.lol"));

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task DecoratesConfiguredRoutePrefixCaseInsensitively()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceRoutes.Add(new RobloxInternalServiceRoute
            {
                Hosts = new List<string> { "www.test.local" },
                PathPrefixes = new List<string> { "/Internal/Avatar" },
            }),
            ctx => ctx.Request.Path = "/internal/avatar/v1/test");

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task DecoratesConfiguredRoutePrefixWithTrailingSlash()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceRoutes.Add(new RobloxInternalServiceRoute
            {
                Hosts = new List<string> { "www.test.local" },
                PathPrefixes = new List<string> { "/apisite/avatar/" },
            }),
            ctx => ctx.Request.Path = "/apisite/avatar/v1/recent-items/all/list");

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task DecoratesRccAuthTypeWhenAccessKeyIsValid()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("www.test.local"),
            ctx => ctx.Request.Headers["accesskey"] = TestConstants.RccAuthorization);

        Assert.True(nextCalled);
        Assert.Equal("rcc", context.Request.Headers[RobloxWebContextConstants.AuthTypeHeaderName]);
    }
    
    [Fact]
    public async Task DecoratesConfiguredNonDotWildcardHostWithProxyHeaders()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("*aapi.local.tld"),
            ctx => ctx.Request.Host = new HostString("testaapi.local.tld"));

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }
    
    [Fact]
    public async Task DoesNotDecorateNonWildcardMatchingHost()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("*aapi.local.tld"),
            ctx => ctx.Request.Host = new HostString("testapi.local.tld")); // missing the extra 'a'

        Assert.True(nextCalled);
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.ProxyAuthorizationHeaderName));
        Assert.NotNull(context.GetRobloxRequestContext());
        Assert.False(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }

    [Fact]
    public async Task ClearsClientSuppliedForwardedIdentityHeadersBeforeDecorating()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("www.test.local"),
            ctx =>
            {
                ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = "client-secret";
                ctx.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = "1";
                ctx.Request.Headers[RobloxWebContextConstants.UsernameHeaderName] = "spoofed";
                ctx.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName] = "spoofed-session";
                ctx.Request.Headers[RobloxWebContextConstants.AccountStatusHeaderName] = "Ok";
            });

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.UserIdHeaderName));
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.UsernameHeaderName));
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.SessionIdHeaderName));
        Assert.False(context.Request.Headers.ContainsKey(RobloxWebContextConstants.AccountStatusHeaderName));
        Assert.False(context.GetRobloxRequestContext()!.IsAuthenticated);
    }

    [Fact]
    public async Task TrustedIncomingProxyHeadersCreateAuthenticatedContextAndAreForwarded()
    {
        var session = InfrastructureTestHelpers.CreateSession(userId: 123, username: "ForwardedUser", sessionId: "forwarded-session");
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("www.test.local"),
            ctx =>
            {
                ctx.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = TestConstants.ProxyAuthorization;
                InfrastructureTestHelpers.AddForwardedSessionHeaders(ctx, session);
            });

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.Equal("123", context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName]);
        Assert.Equal("ForwardedUser", context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName]);
        Assert.Equal("forwarded-session", context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsAuthenticated);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }
}
