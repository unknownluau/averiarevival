using Roblox.Models.Sessions;
using Roblox.Web.Infrastructure.Auth;

namespace Roblox.ApiProxy.Middleware;

public interface IAdminSessionResolver
{
    Task<UserSession?> TryResolveAsync(HttpContext context);
}

public sealed class AdminSessionResolver : IAdminSessionResolver
{
    public async Task<UserSession?> TryResolveAsync(HttpContext context)
    {
        var resolved = await RobloxSessionResolver.TryResolveFromCookie(context);
        return resolved?.Session;
    }
}
