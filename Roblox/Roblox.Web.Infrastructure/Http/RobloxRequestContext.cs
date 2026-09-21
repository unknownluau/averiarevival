using Roblox.Models.Sessions;

namespace Roblox.Web.Infrastructure.Http;

public class RobloxRequestContext
{
    public UserSession? Session { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool IsRobloxClient { get; set; }
    public bool IsRcc { get; set; }
    public bool IsTrustedInternalRequest { get; set; }
    public string? SessionCookie { get; set; }
    public string? DiscordAccessToken { get; set; }
    public string? RobloxAccessToken { get; set; }
    public string RawIp { get; set; } = string.Empty;
    public string HashedIp { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string CurrentGameId { get; set; } = string.Empty;
    public long CurrentPlaceId { get; set; }
}
