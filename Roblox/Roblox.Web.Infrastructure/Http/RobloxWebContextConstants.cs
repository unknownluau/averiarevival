namespace Roblox.Web.Infrastructure.Http;

public static class RobloxWebContextConstants
{
    public const string SessionCookieName = ".AVERISECURITY";
    public const string AltSessionCookieName = ".AVERISECURITY";
    public const string RobloxSessionCookieName = ".ROBLOSECURITY";
    public const string CsrfCookieName = "rbxcsrf4";
    public const string DiscordCookieName = "AVERIA-DISCORD";
    public const string RobloxCookieName = "AVERIA-ROBLOX";
    public const string ProxyAuthorizationHeaderName = "rblx-authorization";
    public const string RequestContextItemKey = "Roblox.Web.Infrastructure.RequestContext";
    public const string LegacySessionItemKey = SessionCookieName;

    public const string UserIdHeaderName = "X-Averia-UserId";
    public const string UsernameHeaderName = "X-Averia-Username";
    public const string SessionIdHeaderName = "X-Averia-SessionId";
    public const string AccountStatusHeaderName = "X-Averia-AccountStatus";
    public const string AuthTypeHeaderName = "X-Averia-AuthType";
    public const string GameIdHeaderName = "X-Averia-GameId";
    public const string PlaceIdHeaderName = "X-Averia-PlaceId";
    public const string ClientIpHashHeaderName = "X-Averia-ClientIpHash";
    public const string UserAgentHeaderName = "X-Averia-UserAgent";
}
