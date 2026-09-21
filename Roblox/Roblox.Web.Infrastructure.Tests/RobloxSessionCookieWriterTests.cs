using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Tests;

public class RobloxSessionCookieWriterTests
{
    [Fact]
    public void AppendSessionCookiesForToken_UsesConfiguredShortBaseUrl()
    {
        var previousShortBaseUrl = Roblox.Configuration.ShortBaseUrl;
        try
        {
            Roblox.Configuration.ShortBaseUrl = "averia.lol";
            var context = InfrastructureTestHelpers.Context();
            context.Request.Host = new HostString("api.averia.lol");

            RobloxSessionCookieWriter.AppendSessionCookiesForToken(context, "session-token");

            var cookies = GetSetCookies(context);
            Assert.Contains(cookies, cookie => cookie.StartsWith(".ROBLOSECURITY=session-token;", StringComparison.Ordinal));
            Assert.All(cookies, cookie => Assert.Contains("domain=.averia.lol", cookie, StringComparison.OrdinalIgnoreCase));
            Assert.All(cookies, cookie => Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Roblox.Configuration.ShortBaseUrl = previousShortBaseUrl;
        }
    }

    [Fact]
    public void AppendSessionCookiesForToken_DerivesRootDomainFromApiHostWhenShortBaseUrlIsMissing()
    {
        var previousShortBaseUrl = Roblox.Configuration.ShortBaseUrl;
        try
        {
            Roblox.Configuration.ShortBaseUrl = string.Empty;
            var context = InfrastructureTestHelpers.Context();
            context.Request.Host = new HostString("api.averia.lol");

            RobloxSessionCookieWriter.AppendSessionCookiesForToken(context, "session-token");

            var cookies = GetSetCookies(context);
            Assert.Contains(cookies, cookie => cookie.StartsWith(".AVERISECURITY=session-token;", StringComparison.Ordinal));
            Assert.All(cookies, cookie => Assert.Contains("domain=.averia.lol", cookie, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Roblox.Configuration.ShortBaseUrl = previousShortBaseUrl;
        }
    }

    [Fact]
    public void AppendSessionCookiesForToken_OmitsDomainForLocalhostWhenShortBaseUrlIsMissing()
    {
        var previousShortBaseUrl = Roblox.Configuration.ShortBaseUrl;
        try
        {
            Roblox.Configuration.ShortBaseUrl = string.Empty;
            var context = InfrastructureTestHelpers.Context();
            context.Request.Host = new HostString("localhost");

            RobloxSessionCookieWriter.AppendSessionCookiesForToken(context, "session-token");

            var cookies = GetSetCookies(context);
            Assert.Equal(2, cookies.Count);
            Assert.All(cookies, cookie => Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Roblox.Configuration.ShortBaseUrl = previousShortBaseUrl;
        }
    }

    [Fact]
    public void DeleteSessionCookies_ExpiresAllSessionCookieNames()
    {
        var previousShortBaseUrl = Roblox.Configuration.ShortBaseUrl;
        try
        {
            Roblox.Configuration.ShortBaseUrl = "averia.lol";
            var context = InfrastructureTestHelpers.Context();
            context.Request.Host = new HostString("api.averia.lol");

            RobloxSessionCookieWriter.DeleteSessionCookies(context);

            var cookies = GetSetCookies(context);
            Assert.Contains(cookies, cookie => IsExpiredCookie(cookie, RobloxWebContextConstants.RobloxSessionCookieName));
            Assert.Contains(cookies, cookie => IsExpiredCookie(cookie, RobloxWebContextConstants.SessionCookieName));
            Assert.Contains(cookies, cookie => IsExpiredCookie(cookie, RobloxWebContextConstants.AltSessionCookieName));
            Assert.All(cookies, cookie => Assert.Contains("domain=.averia.lol", cookie, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Roblox.Configuration.ShortBaseUrl = previousShortBaseUrl;
        }
    }

    private static IReadOnlyList<string> GetSetCookies(HttpContext context)
    {
        return context.Response.Headers.SetCookie.Select(cookie => cookie ?? string.Empty).ToList();
    }

    private static bool IsExpiredCookie(string cookie, string name)
    {
        return cookie.StartsWith(name + "=;", StringComparison.Ordinal) &&
               cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase);
    }
}
