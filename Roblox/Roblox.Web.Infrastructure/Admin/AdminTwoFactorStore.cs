namespace Roblox.Web.Infrastructure.Admin;

public sealed class AdminTwoFactorStore : IAdminTwoFactorStore
{
    public Task<bool> IsVerifiedAsync(long userId, string sessionId)
    {
        return AdminTwoFactorVerification.IsVerifiedAsync(userId, sessionId);
    }

    public Task MarkVerifiedAsync(long userId, string sessionId)
    {
        return AdminTwoFactorVerification.MarkVerifiedAsync(userId, sessionId);
    }

    public Task InvalidateAsync(long userId, string sessionId)
    {
        return AdminTwoFactorVerification.InvalidateAsync(userId, sessionId);
    }
}
