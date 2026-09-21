using Microsoft.AspNetCore.Http;
using Roblox.Models.Sessions;

namespace Roblox.Web.Infrastructure.Http;

public static class RobloxHttpContextExtensions
{
    public static RobloxRequestContext? GetRobloxRequestContext(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(RobloxWebContextConstants.RequestContextItemKey, out var value))
        {
            return value as RobloxRequestContext;
        }

        return null;
    }

    public static void SetRobloxRequestContext(this HttpContext httpContext, RobloxRequestContext context)
    {
        httpContext.Items[RobloxWebContextConstants.RequestContextItemKey] = context;
        if (context.Session != null)
        {
            httpContext.Items[RobloxWebContextConstants.LegacySessionItemKey] = context.Session;
        }
        else
        {
            httpContext.Items.Remove(RobloxWebContextConstants.LegacySessionItemKey);
        }
    }

    public static UserSession? GetLegacyRobloxSession(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(RobloxWebContextConstants.LegacySessionItemKey, out var value))
        {
            return value as UserSession;
        }

        return null;
    }
}
