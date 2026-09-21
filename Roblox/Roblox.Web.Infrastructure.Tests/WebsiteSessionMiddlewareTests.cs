using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;
using Roblox.Website.Middleware;

namespace Roblox.Web.Infrastructure.Tests;

public class WebsiteSessionMiddlewareTests
{
    [Fact]
    public async Task RobloxSessionCookie_IsProcessedByWebsiteMiddleware()
    {
        var nextCalled = false;
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie =
            $"{RobloxWebContextConstants.RobloxSessionCookieName}=not-a-valid-session-token";

        var middleware = new SessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        var deletedCookies = context.Response.Headers.SetCookie.ToArray();
        Assert.Contains(deletedCookies, value =>
            value!.StartsWith(RobloxWebContextConstants.RobloxSessionCookieName + "=", StringComparison.Ordinal));
        Assert.Contains(deletedCookies, value =>
            value!.StartsWith(RobloxWebContextConstants.SessionCookieName + "=", StringComparison.Ordinal));
        Assert.Contains(deletedCookies, value =>
            value!.StartsWith(RobloxWebContextConstants.AltSessionCookieName + "=", StringComparison.Ordinal));
    }
}
