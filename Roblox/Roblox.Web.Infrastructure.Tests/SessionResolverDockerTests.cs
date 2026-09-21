using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class SessionResolverDockerTests
{
    [Fact]
    public async Task DockerInfrastructure_IsAvailableWhenRequired()
    {
        if (!DockerInfrastructureFixture.DockerTestsRequired)
        {
            return;
        }

        Assert.True(await DockerInfrastructureFixture.IsInfrastructureAvailableAsync());
    }

    [Theory]
    [InlineData(RobloxWebContextConstants.SessionCookieName)]
    [InlineData(RobloxWebContextConstants.AltSessionCookieName)]
    [InlineData(RobloxWebContextConstants.RobloxSessionCookieName)]
    public async Task ResolvesValidSignedSessionCookie(string cookieName)
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var seeded = await fixture.CreateSeededSessionAsync();
        var context = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddCookie(context, cookieName, seeded.Cookie);

        var resolved = await RobloxSessionResolver.TryResolveFromCookie(context);

        Assert.NotNull(resolved);
        Assert.Equal(seeded.Cookie, resolved!.EncodedCookie);
        Assert.Equal(seeded.UserId, resolved.Session.userId);
        Assert.Equal(seeded.Username, resolved.Session.username);
        Assert.Equal(seeded.SessionId, resolved.Session.sessionId);
        Assert.Equal(seeded.UserId, resolved.UserInfo.userId);
    }

    [Fact]
    public async Task ReturnsNullForMissingEmptyAndTamperedCookies()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var missing = InfrastructureTestHelpers.Context();
        var empty = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddCookie(empty, RobloxWebContextConstants.SessionCookieName, "");
        var tampered = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddCookie(tampered, RobloxWebContextConstants.SessionCookieName, "not-a-valid-jwt");

        Assert.Null(await RobloxSessionResolver.TryResolveFromCookie(missing));
        Assert.Null(await RobloxSessionResolver.TryResolveFromCookie(empty));
        Assert.Null(await RobloxSessionResolver.TryResolveFromCookie(tampered));
    }

    [Fact]
    public async Task ReturnsNullForDeletedSession()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var seeded = await fixture.CreateSeededSessionAsync();
        await fixture.DeleteSessionAsync(seeded.SessionId);
        var context = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddCookie(context, RobloxWebContextConstants.SessionCookieName, seeded.Cookie);

        Assert.Null(await RobloxSessionResolver.TryResolveFromCookie(context));
    }

    [Fact]
    public async Task ReturnsNullForExpiredSession()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var seeded = await fixture.CreateSeededSessionAsync();
        await fixture.ExpireSessionsAsync(seeded.UserId);
        var context = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddCookie(context, RobloxWebContextConstants.SessionCookieName, seeded.Cookie);

        Assert.Null(await RobloxSessionResolver.TryResolveFromCookie(context));
    }

    [Fact]
    public async Task ApiProxyForwardsResolvedSessionHeadersForInternalRoutes()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var seeded = await fixture.CreateSeededSessionAsync();
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceHosts.Add("www.test.local"),
            ctx => InfrastructureTestHelpers.AddCookie(ctx, RobloxWebContextConstants.SessionCookieName, seeded.Cookie));

        Assert.True(nextCalled);
        Assert.Equal(seeded.UserId.ToString(), context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName]);
        Assert.Equal(seeded.Username, context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName]);
        Assert.Equal(seeded.SessionId, context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsAuthenticated);
    }

    [Fact]
    public async Task ApiProxyForwardsResolvedSessionHeadersForAvatarApisiteRoute()
    {
        var fixture = await DockerInfrastructureFixture.CreateAsync();
        if (fixture == null)
        {
            return;
        }

        var seeded = await fixture.CreateSeededSessionAsync();
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeApiProxyForwardedAuthAsync(
            options => options.InternalServiceRoutes.Add(new RobloxInternalServiceRoute
            {
                Hosts = new List<string> { "www.test.local" },
                PathPrefixes = new List<string> { "/apisite/avatar/" },
            }),
            ctx =>
            {
                ctx.Request.Host = new HostString("www.test.local");
                ctx.Request.Path = "/apisite/avatar/v1/recent-items/all/list";
                InfrastructureTestHelpers.AddCookie(ctx, RobloxWebContextConstants.SessionCookieName, seeded.Cookie);
            });

        Assert.True(nextCalled);
        Assert.Equal(TestConstants.ProxyAuthorization, context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName]);
        Assert.Equal(seeded.UserId.ToString(), context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName]);
        Assert.Equal(seeded.Username, context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName]);
        Assert.Equal(seeded.SessionId, context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName]);
        Assert.Equal("browser", context.Request.Headers[RobloxWebContextConstants.AuthTypeHeaderName]);
        Assert.True(context.GetRobloxRequestContext()!.IsAuthenticated);
        Assert.True(context.GetRobloxRequestContext()!.IsTrustedInternalRequest);
    }
}
