using System.Text;
using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class RobloxRequestContextFactoryTests
{
    [Fact]
    public void CreateAnonymous_UsesStableDefaults()
    {
        var context = InfrastructureTestHelpers.Context();
        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "hash";

        var requestContext = RobloxRequestContextFactory.CreateAnonymous(context, TestConstants.RccAuthorization);

        Assert.False(requestContext.IsAuthenticated);
        Assert.False(requestContext.IsTrustedInternalRequest);
        Assert.False(requestContext.IsRcc);
        Assert.False(requestContext.IsRobloxClient);
        Assert.Null(requestContext.Session);
        Assert.Equal("hash", requestContext.HashedIp);
        Assert.Equal("127.0.0.1", requestContext.RawIp);
    }

    [Fact]
    public void CreateAnonymous_PreservesLegacySessionItem()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        var session = InfrastructureTestHelpers.CreateSession();
        httpContext.SetRobloxRequestContext(new RobloxRequestContext
        {
            Session = session,
            IsAuthenticated = true,
        });

        var requestContext = RobloxRequestContextFactory.CreateAnonymous(httpContext, TestConstants.RccAuthorization);

        Assert.True(requestContext.IsAuthenticated);
        Assert.Same(session, requestContext.Session);
    }

    [Fact]
    public void CreateFromForwardedHeaders_CreatesAuthenticatedSession()
    {
        var context = InfrastructureTestHelpers.Context();
        InfrastructureTestHelpers.AddForwardedSessionHeaders(context);

        var requestContext = RobloxRequestContextFactory.CreateFromForwardedHeaders(context, true, TestConstants.RccAuthorization);

        Assert.True(requestContext.IsAuthenticated);
        Assert.True(requestContext.IsTrustedInternalRequest);
        Assert.NotNull(requestContext.Session);
        Assert.Equal(123, requestContext.Session!.userId);
        Assert.Equal("InfrastructureUser", requestContext.Session.username);
        Assert.Equal("session-id", requestContext.Session.sessionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void CreateFromForwardedHeaders_LeavesMalformedUserIdAnonymous(string userId)
    {
        var context = InfrastructureTestHelpers.Context();
        context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = userId;
        context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName] = "TestUser";
        context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName] = "session";

        var requestContext = RobloxRequestContextFactory.CreateFromForwardedHeaders(context, true, TestConstants.RccAuthorization);

        Assert.False(requestContext.IsAuthenticated);
        Assert.Null(requestContext.Session);
    }

    [Fact]
    public void CreateFromForwardedHeaders_LeavesMissingSessionHeadersAnonymous()
    {
        var context = InfrastructureTestHelpers.Context();
        context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = "123";

        var requestContext = RobloxRequestContextFactory.CreateFromForwardedHeaders(context, true, TestConstants.RccAuthorization);

        Assert.False(requestContext.IsAuthenticated);
        Assert.Null(requestContext.Session);
    }

    [Fact]
    public void CreateFromForwardedHeaders_InterpretsForwardedClientRccGameAndPlaceSignals()
    {
        var context = InfrastructureTestHelpers.Context();
        context.Request.Headers.UserAgent = "Mozilla/5.0";
        context.Request.Headers[RobloxWebContextConstants.UserAgentHeaderName] = "Roblox/WinInet";
        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "proxy-hash";
        context.Request.Headers[RobloxWebContextConstants.AuthTypeHeaderName] = "rcc roblox";
        context.Request.Headers[RobloxWebContextConstants.GameIdHeaderName] = "game-123";
        context.Request.Headers[RobloxWebContextConstants.PlaceIdHeaderName] = "456";

        var requestContext = RobloxRequestContextFactory.CreateFromForwardedHeaders(context, true, TestConstants.RccAuthorization);

        Assert.True(requestContext.IsRobloxClient);
        Assert.True(requestContext.IsRcc);
        Assert.Equal("Roblox/WinInet", requestContext.UserAgent);
        Assert.Equal("proxy-hash", requestContext.HashedIp);
        Assert.Equal("game-123", requestContext.CurrentGameId);
        Assert.Equal(456, requestContext.CurrentPlaceId);
    }

    [Fact]
    public void CreateAnonymous_DetectsRccByAccessKeyAndRobloxByUserAgent()
    {
        var context = InfrastructureTestHelpers.Context();
        context.Request.Headers.UserAgent = "Roblox/WinInet";
        context.Request.Headers["accesskey"] = TestConstants.RccAuthorization;
        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "proxy-hash";

        var requestContext = RobloxRequestContextFactory.CreateAnonymous(context, TestConstants.RccAuthorization);

        Assert.True(requestContext.IsRobloxClient);
        Assert.True(requestContext.IsRcc);
    }

    [Fact]
    public void CreateAnonymous_DecodesDiscordTokenFromHeaderAndIgnoresInvalidBase64()
    {
        var valid = InfrastructureTestHelpers.Context();
        valid.Request.Headers[RobloxWebContextConstants.DiscordCookieName] = Convert.ToBase64String(Encoding.UTF8.GetBytes("discord-token"));
        valid.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "hash";

        var invalid = InfrastructureTestHelpers.Context();
        invalid.Request.Headers[RobloxWebContextConstants.DiscordCookieName] = "not base64";
        invalid.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "hash";

        Assert.Equal("discord-token", RobloxRequestContextFactory.CreateAnonymous(valid).DiscordAccessToken);
        Assert.Null(RobloxRequestContextFactory.CreateAnonymous(invalid).DiscordAccessToken);
    }

    [Fact]
    public void ApplyToHttpContext_StoresRequestContextAndLegacySession()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        var session = InfrastructureTestHelpers.CreateSession();
        var requestContext = new RobloxRequestContext
        {
            Session = session,
            IsAuthenticated = true,
        };

        RobloxRequestContextFactory.ApplyToHttpContext(httpContext, requestContext);

        Assert.Same(requestContext, httpContext.GetRobloxRequestContext());
        Assert.Same(session, httpContext.GetLegacyRobloxSession());
    }
}
