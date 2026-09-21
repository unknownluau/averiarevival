using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class RobloxRequestContextAccessorTests
{
    [Fact]
    public void Current_ReturnsEmptyContextWithoutHttpContext()
    {
        var accessor = new RobloxRequestContextAccessor(new HttpContextAccessor(), InfrastructureTestHelpers.Options());

        var context = accessor.Current;

        Assert.False(context.IsAuthenticated);
        Assert.Null(context.Session);
    }

    [Fact]
    public void Current_CreatesAndStoresOneContextPerRequest()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        httpContext.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = "hash";
        var accessor = InfrastructureTestHelpers.RequestContextAccessor(httpContext);

        var first = accessor.Current;
        var second = accessor.Current;

        Assert.Same(first, second);
        Assert.Same(first, httpContext.GetRobloxRequestContext());
    }

    [Fact]
    public void SetCurrent_UpdatesContextAndLegacySessionItems()
    {
        var httpContext = InfrastructureTestHelpers.Context();
        var accessor = InfrastructureTestHelpers.RequestContextAccessor(httpContext);
        var session = InfrastructureTestHelpers.CreateSession();

        accessor.SetCurrent(new RobloxRequestContext
        {
            Session = session,
            IsAuthenticated = true,
        });

        Assert.Same(session, httpContext.GetLegacyRobloxSession());

        accessor.SetCurrent(new RobloxRequestContext());

        Assert.Null(httpContext.GetLegacyRobloxSession());
        Assert.False(accessor.Current.IsAuthenticated);
    }
}
