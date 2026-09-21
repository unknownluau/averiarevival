using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Admin;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminStaffFilterAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var requestContext = context.HttpContext.GetRobloxRequestContext();
        var session = requestContext?.Session;
        if (session == null)
        {
            await SendForbiddenAsync(context.HttpContext);
            return;
        }

        var staff = context.HttpContext.RequestServices.GetRequiredService<IAdminStaffAuthorizationService>();
        if (!await staff.IsStaffAsync(session.userId))
        {
            await SendForbiddenAsync(context.HttpContext);
            return;
        }

        await next();
    }

    public static async Task SendForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            errors = new[]
            {
                new
                {
                    message = "Forbidden",
                    code = 0,
                },
            },
        });
    }
}
