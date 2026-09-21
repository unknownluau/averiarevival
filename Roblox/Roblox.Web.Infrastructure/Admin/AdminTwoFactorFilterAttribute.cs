using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Admin;

[AttributeUsage(AttributeTargets.Method)]
public sealed class SkipAdminTwoFactorAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminTwoFactorFilterAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<SkipAdminTwoFactorAttribute>().Any())
        {
            await next();
            return;
        }

        var requestContext = context.HttpContext.GetRobloxRequestContext();
        var session = requestContext?.Session;
        var store = context.HttpContext.RequestServices.GetRequiredService<IAdminTwoFactorStore>();

        if (session == null || !await store.IsVerifiedAsync(session.userId, session.sessionId))
        {
            context.Result = new JsonResult(new { error = "2FA verification required" })
            {
                StatusCode = StatusCodes.Status401Unauthorized,
            };
            return;
        }

        await next();
    }
}
