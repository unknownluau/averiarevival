using Microsoft.Extensions.Configuration;
using Roblox.Models.Staff;
using Roblox.Services;

namespace Roblox.Web.Infrastructure.Admin;

public sealed class AdminStaffAuthorizationService : IAdminStaffAuthorizationService
{
    private readonly IReadOnlySet<long> _ownerUserIds;

    public AdminStaffAuthorizationService(IConfiguration configuration)
    {
        _ownerUserIds = configuration
            .GetSection("OwnerUserId")
            .Get<List<long>>()?
            .ToHashSet() ?? new HashSet<long>();
    }

    public bool IsOwner(long userId)
    {
        return _ownerUserIds.Contains(userId);
    }

    public async Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId)
    {
        if (IsOwner(userId))
        {
            return Enum.GetValues<Access>();
        }

        using var users = ServiceProvider.GetOrCreate<UsersService>();
        return (await users.GetStaffPermissions(userId))
            .Select(permission => permission.permission)
            .ToList();
    }

    public async Task<bool> IsStaffAsync(long userId)
    {
        return IsOwner(userId) || (await GetPermissionsAsync(userId)).Any();
    }
}
