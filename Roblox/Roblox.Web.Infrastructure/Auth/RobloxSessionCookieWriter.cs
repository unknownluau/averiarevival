using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Auth;

public static class RobloxSessionCookieWriter
{
    public static string AppendSessionCookies(HttpContext httpContext, string sessionId, TimeSpan? lifetime = null)
    {
        var sessionCookie = RobloxSessionTokenCodec.CreateJwt(new SessionTokenPayload
        {
            sessionId = sessionId,
            createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
        });

        AppendSessionCookiesForToken(httpContext, sessionCookie, lifetime);
        return sessionCookie;
    }

    public static void AppendSessionCookiesForToken(HttpContext httpContext, string sessionCookie, TimeSpan? lifetime = null)
    {
        var options = CreateSessionCookieOptions(httpContext, lifetime);
        httpContext.Response.Cookies.Append(RobloxWebContextConstants.RobloxSessionCookieName, sessionCookie, options);
        httpContext.Response.Cookies.Append(RobloxWebContextConstants.SessionCookieName, sessionCookie, CreateSessionCookieOptions(httpContext, lifetime));
    }

    public static void DeleteSessionCookies(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(
            RobloxWebContextConstants.RobloxSessionCookieName,
            CreateSessionCookieOptions(httpContext));
        httpContext.Response.Cookies.Delete(
            RobloxWebContextConstants.SessionCookieName,
            CreateSessionCookieOptions(httpContext));
        httpContext.Response.Cookies.Delete(
            RobloxWebContextConstants.AltSessionCookieName,
            CreateSessionCookieOptions(httpContext));
    }

    private static CookieOptions CreateSessionCookieOptions(HttpContext httpContext, TimeSpan? lifetime = null)
    {
        var options = new CookieOptions
        {
            Secure = false,
            Expires = DateTimeOffset.Now.Add(lifetime ?? TimeSpan.FromDays(14)),
            IsEssential = true,
            HttpOnly = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        };

        var domain = ResolveCookieDomain(httpContext);
        if (!string.IsNullOrWhiteSpace(domain))
        {
            options.Domain = domain;
        }

        return options;
    }

    private static string? ResolveCookieDomain(HttpContext httpContext)
    {
        var configuredBaseUrl = Roblox.Configuration.ShortBaseUrl;
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return "." + configuredBaseUrl.Trim().TrimStart('.');
        }

        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host) ||
            string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            System.Net.IPAddress.TryParse(host, out _))
        {
            return null;
        }

        var labels = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
        {
            return null;
        }

        var rootDomain = string.Join('.', labels[^2], labels[^1]);
        return "." + rootDomain;
    }
}
