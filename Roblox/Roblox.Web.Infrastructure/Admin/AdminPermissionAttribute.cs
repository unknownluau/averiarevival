using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Models.Staff;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Admin;

[AttributeUsage(AttributeTargets.Method)]
public sealed class AdminPermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly Access _permission;

    public AdminPermissionAttribute(Access permission)
    {
        _permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!Enum.IsDefined(_permission))
        {
            await AdminStaffFilterAttribute.SendForbiddenAsync(context.HttpContext);
            return;
        }

        var requestContext = context.HttpContext.GetRobloxRequestContext();
        var session = requestContext?.Session;
        if (session == null)
        {
            await AdminStaffFilterAttribute.SendForbiddenAsync(context.HttpContext);
            return;
        }

        var staff = context.HttpContext.RequestServices.GetRequiredService<IAdminStaffAuthorizationService>();
        if (staff.IsOwner(session.userId))
        {
            await next();
            return;
        }

        var permissions = await staff.GetPermissionsAsync(session.userId);
        if (!permissions.Contains(_permission))
        {
            await AdminStaffFilterAttribute.SendForbiddenAsync(context.HttpContext);
            return;
        }

        await next();
    }
}
