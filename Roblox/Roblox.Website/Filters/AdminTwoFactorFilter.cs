using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Roblox.Cache;
using Roblox.Models.Sessions;
using Roblox.Web.Infrastructure.Admin;
using Roblox.Website.Middleware;
using Roblox.Website.WebsiteModels;

[AttributeUsage(AttributeTargets.Method)]
public class SkipAdminTwoFactorAttribute : Attribute { };

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminTwoFactorFilter : Attribute, IAsyncActionFilter
{
    public static string GetKey(long userId, string sessionId) => AdminTwoFactorVerification.GetKey(userId, sessionId);

    public static async Task<bool> IsVerified(long userId, string sessionId)
    {
        return await AdminTwoFactorVerification.IsVerifiedAsync(userId, sessionId);
    }

    public static async Task MarkVerified(long userId, string sessionId)
    {
        await AdminTwoFactorVerification.MarkVerifiedAsync(userId, sessionId);
    }
    public static async Task Invalidate(long userId, string sessionId)
    {
        await AdminTwoFactorVerification.InvalidateAsync(userId, sessionId);
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var skip = context.ActionDescriptor.EndpointMetadata.OfType<SkipAdminTwoFactorAttribute>().Any();
        if (skip)
        {
            await next();
            return;
        }

        var session = context.HttpContext.Items[SessionMiddleware.CookieName] as UserSession;
        if (session == null || !await IsVerified(session.userId, session.sessionId))
        {
            var isApi = context.HttpContext.Request.Path.StartsWithSegments("/admin-api");
            if (isApi)
                context.Result = new JsonResult(new { error = "2FA verification required" }) { StatusCode = 401 };
            else
                context.Result = new RedirectResult("/admin-api/api/2fa");
            return;
        }
        await next();
    }
}
