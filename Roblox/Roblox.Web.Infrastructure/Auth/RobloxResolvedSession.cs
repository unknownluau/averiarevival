using Roblox.Dto.Users;
using Roblox.Models.Sessions;

namespace Roblox.Web.Infrastructure.Auth;

public class RobloxResolvedSession
{
    public required UserSession Session { get; init; }
    public required DateTime SessionCreatedAt { get; init; }
    public required UserInfo UserInfo { get; init; }
    public required string EncodedCookie { get; init; }
}
