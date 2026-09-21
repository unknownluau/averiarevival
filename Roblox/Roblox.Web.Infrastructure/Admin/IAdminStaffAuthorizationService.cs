using Roblox.Models.Staff;

namespace Roblox.Web.Infrastructure.Admin;

public interface IAdminStaffAuthorizationService
{
    bool IsOwner(long userId);

    Task<IReadOnlyCollection<Access>> GetPermissionsAsync(long userId);

    Task<bool> IsStaffAsync(long userId);
}
