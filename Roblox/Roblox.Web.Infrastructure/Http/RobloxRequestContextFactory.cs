using System.Text;
using Microsoft.AspNetCore.Http;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Auth;

namespace Roblox.Web.Infrastructure.Http;

public static class RobloxRequestContextFactory
{
    public static RobloxRequestContext CreateAnonymous(HttpContext httpContext, string? rccAuthorization = null)
    {
        var rawIp = TryGetRawIp(httpContext);
        var hashedIp = httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.ClientIpHashHeaderName, out var forwardedIpHash)
            ? forwardedIpHash.ToString()
            : (string.IsNullOrWhiteSpace(rawIp) ? string.Empty : RobloxIpHasher.GetIp(rawIp));

        var userAgent = httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.UserAgentHeaderName, out var forwardedUserAgent)
            ? forwardedUserAgent.ToString()
            : httpContext.Request.Headers.UserAgent.ToString();

        return new RobloxRequestContext
        {
            Session = httpContext.GetLegacyRobloxSession(),
            IsAuthenticated = httpContext.GetLegacyRobloxSession() != null,
            IsRobloxClient = IsRobloxClient(userAgent, httpContext),
            IsRcc = IsRccRequest(httpContext, rccAuthorization),
            SessionCookie = RobloxSessionResolver.GetCookieValue(httpContext),
            DiscordAccessToken = TryGetDiscordAccessToken(httpContext),
            RobloxAccessToken = TryGetRobloxAccessToken(httpContext),
            RawIp = rawIp,
            HashedIp = hashedIp,
            UserAgent = userAgent,
            CurrentGameId = GetCurrentGameId(httpContext),
            CurrentPlaceId = GetCurrentPlaceId(httpContext),
            IsTrustedInternalRequest = false,
        };
    }

    public static RobloxRequestContext CreateWithSession(HttpContext httpContext, UserSession session, string? rccAuthorization = null)
    {
        var context = CreateAnonymous(httpContext, rccAuthorization);
        context.Session = session;
        context.IsAuthenticated = true;
        return context;
    }

    public static RobloxRequestContext CreateFromForwardedHeaders(HttpContext httpContext, bool isTrustedInternalRequest, string? rccAuthorization = null)
    {
        var context = CreateAnonymous(httpContext, rccAuthorization);
        context.IsTrustedInternalRequest = isTrustedInternalRequest;

        if (httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.AuthTypeHeaderName, out var authType))
        {
            var authTypeValue = authType.ToString().ToLowerInvariant();
            context.IsRobloxClient = context.IsRobloxClient || authTypeValue.Contains("roblox");
            context.IsRcc = context.IsRcc || authTypeValue.Contains("rcc");
        }

        if (!httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.UserIdHeaderName, out var userIdHeader) ||
            !long.TryParse(userIdHeader.ToString(), out var userId) ||
            !httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.UsernameHeaderName, out var usernameHeader) ||
            !httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.SessionIdHeaderName, out var sessionIdHeader))
        {
            return context;
        }

        var accountStatus = AccountStatus.Ok;
        if (httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.AccountStatusHeaderName, out var accountStatusHeader))
        {
            Enum.TryParse(accountStatusHeader.ToString(), true, out accountStatus);
        }

        context.Session = new UserSession(
            userId,
            usernameHeader.ToString(),
            DateTime.UtcNow,
            accountStatus,
            0,
            false,
            sessionIdHeader.ToString());
        context.IsAuthenticated = true;
        return context;
    }

    public static void ApplyToHttpContext(HttpContext httpContext, RobloxRequestContext context)
    {
        httpContext.SetRobloxRequestContext(context);
    }

    private static string TryGetRawIp(HttpContext httpContext)
    {
        try
        {
            return RobloxIpHasher.GetRequesterIpRaw(httpContext);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetCurrentGameId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.GameIdHeaderName, out var forwardedGameId))
        {
            return forwardedGameId.ToString();
        }

        if (httpContext.Request.Headers.TryGetValue("Roblox-Game-Id", out var gameId))
        {
            return gameId.ToString();
        }

        return string.Empty;
    }

    private static long GetCurrentPlaceId(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.PlaceIdHeaderName, out var forwardedPlaceId) &&
            long.TryParse(forwardedPlaceId.ToString(), out var parsedForwardedPlaceId))
        {
            return parsedForwardedPlaceId;
        }

        if (httpContext.Request.Headers.TryGetValue("Roblox-Place-Id", out var placeId) &&
            long.TryParse(placeId.ToString(), out var parsedPlaceId))
        {
            return parsedPlaceId;
        }

        return 0;
    }

    private static bool IsRobloxClient(string userAgent, HttpContext httpContext)
    {
        if (!string.IsNullOrWhiteSpace(userAgent) && userAgent.Contains("roblox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (httpContext.Request.Headers.TryGetValue(RobloxWebContextConstants.AuthTypeHeaderName, out var authType))
        {
            return authType.ToString().Contains("roblox", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsRccRequest(HttpContext httpContext, string? rccAuthorization)
    {
        var accessKey = httpContext.Request.Headers.ContainsKey("accesskey")
            ? httpContext.Request.Headers["accesskey"].ToString()
            : null;
        var expectedAuthorization = !string.IsNullOrWhiteSpace(rccAuthorization)
            ? rccAuthorization
            : Roblox.Configuration.RccAuthorization;
        return !string.IsNullOrWhiteSpace(expectedAuthorization) && accessKey == expectedAuthorization;
    }

    private static string? TryGetDiscordAccessToken(HttpContext httpContext)
    {
        var tokenEncoded = httpContext.Request.Headers.ContainsKey(RobloxWebContextConstants.DiscordCookieName)
            ? httpContext.Request.Headers[RobloxWebContextConstants.DiscordCookieName].ToString()
            : httpContext.Request.Cookies[RobloxWebContextConstants.DiscordCookieName];

        if (string.IsNullOrWhiteSpace(tokenEncoded))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(tokenEncoded));
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetRobloxAccessToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RobloxWebContextConstants.RobloxCookieName, out var tokenKey) || string.IsNullOrWhiteSpace(tokenKey))
        {
            return null;
        }

        return Roblox.Services.Cache.distributed.StringGet($"{RobloxWebContextConstants.RobloxCookieName}:{tokenKey}");
    }
}
