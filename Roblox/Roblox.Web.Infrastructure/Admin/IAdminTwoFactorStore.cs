namespace Roblox.Web.Infrastructure.Admin;

public interface IAdminTwoFactorStore
{
    Task<bool> IsVerifiedAsync(long userId, string sessionId);

    Task MarkVerifiedAsync(long userId, string sessionId);

    Task InvalidateAsync(long userId, string sessionId);
}
