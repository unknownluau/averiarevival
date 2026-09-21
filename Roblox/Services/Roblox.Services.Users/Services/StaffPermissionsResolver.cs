using Microsoft.Extensions.Configuration;
using Roblox.Models.Staff;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Services.Users.Services;

public sealed class StaffPermissionsResolver
{
    private readonly IReadOnlySet<long> _ownerUserIds;

    public StaffPermissionsResolver(IConfiguration configuration)
    {
        _ownerUserIds = configuration
            .GetSection("OwnerUserId")
            .Get<List<long>>()?
            .ToHashSet() ?? new HashSet<long>();
    }

    public async Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId, RobloxServiceAccessor services)
    {
        if (_ownerUserIds.Contains(userId))
        {
            return Enum.GetValues<Access>();
        }

        return (await services.users.GetStaffPermissions(userId))
            .Select(permission => permission.permission)
            .ToList();
    }
}
