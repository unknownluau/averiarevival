namespace Roblox.Web.Infrastructure.Auth;

public class SessionTokenPayload
{
    public string sessionId { get; set; } = string.Empty;
    public long createdAt { get; set; }
}
