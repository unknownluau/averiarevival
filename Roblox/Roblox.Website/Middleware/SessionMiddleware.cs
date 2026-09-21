using System.Text.Json;
using JWT.Exceptions;
using Microsoft.AspNetCore.Http.Extensions;
using Roblox.Dto.Users;
using Roblox.Models.Sessions;
using Roblox.Models.Users;
using Roblox.Services;
using Roblox.Services.Exceptions;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Http;
using Roblox.Website.Controllers;
using Roblox.Website.Filters;
using Roblox.Website.Lib;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Website.Middleware;

public class JwtEntry
{
    public string sessionId { get; set; }
    public long createdAt { get; set; }
}

public class SessionMiddleware
{
    private RequestDelegate _next;
    public const string CookieName = RobloxWebContextConstants.SessionCookieName;
    public const string AltCookieName = RobloxWebContextConstants.AltSessionCookieName;

    public static void Configure(string newJwtKey)
    {
        RobloxSessionTokenCodec.Configure(newJwtKey);
    }


    public SessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static string CreateJwt<T>(T obj)
    {
        return RobloxSessionTokenCodec.CreateJwt(obj);
    }

    public static T DecodeJwt<T>(string token)
    {
        return RobloxSessionTokenCodec.DecodeJwt<T>(token);
    }

    private async Task OnBadSession(HttpContext ctx)
    {
        RobloxSessionCookieWriter.DeleteSessionCookies(ctx);
        await _next(ctx);
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var authTimer = new MiddlewareTimer(ctx, "au");
        var currentPath = ctx.Request.Path.ToString().ToLower();
        ctx.SetRobloxRequestContext(RobloxRequestContextFactory.CreateAnonymous(ctx));
        try
        {
            // Keep legacy Website authentication aligned with all shared aliases, including .ROBLOSECURITY.
            if (RobloxSessionResolver.GetCookieValue(ctx) != null)
            {
                var resolved = await RobloxSessionResolver.TryResolveFromCookie(ctx);
                if (resolved == null)
                {
                    authTimer.Stop();
                    await OnBadSession(ctx);
                    return;
                }

                using var users = ServiceProvider.GetOrCreate<UsersService>();
                var userInfo = resolved.UserInfo;
                if (userInfo.accountStatus is AccountStatus.Forgotten or AccountStatus.MustValidateEmail)
                {
                    authTimer.Stop();
                    await OnBadSession(ctx);
                    return;
                }

                ctx.SetRobloxRequestContext(RobloxRequestContextFactory.CreateWithSession(ctx, resolved.Session));

                if (userInfo.accountStatus is AccountStatus.Suppressed or AccountStatus.Poisoned
                    or AccountStatus.Deleted)
                {
                    if (!currentPath.StartsWith("/auth/"))
                    {
                        authTimer.Stop();
                        ctx.Response.StatusCode = 302;
                        ctx.Response.Headers.Append("location", "/auth/notapproved");
                        return;
                    }
                }

                var appStatus = await users.IsUserApproved(userInfo.userId);
                if (!appStatus && !userInfo.isAdmin && !userInfo.isModerator && !StaffFilter.IsOwner(userInfo.userId))
                {
                    if (!currentPath.StartsWith("/auth/"))
                    {
                        authTimer.Stop();
                        ctx.Response.StatusCode = 302;
                        ctx.Response.Headers.Append("location", "/");
                        return;
                    }
                }

                if (!currentPath.StartsWith("/thumbs/") && !currentPath.StartsWith("/images/"))
                {
                    await users.EarnDailyRobuxNoVirusNoScamHindiSubtitles(userInfo.userId, await StaffFilter.IsStaff(userInfo.userId));
                    await users.EarnDailyTickets(userInfo.userId);
                    if (users.TrySetOnlineTimeUpdated(userInfo.userId))
                    {
                        await users.UpdateOnlineStatus(userInfo.userId);
                    }
                }

                if (currentPath == "/")
                {
                    ctx.Response.StatusCode = 302;
                    ctx.Response.Headers.Append("location", "/home");
                    return;
                }

                authTimer.Stop();
            }
        }
        catch (System.Exception e) when (e is InvalidTokenPartsException or NullReferenceException or FormatException or SignatureVerificationException)
        {
            ctx.Response.Cookies.Delete(CookieName);
            ctx.SetRobloxRequestContext(RobloxRequestContextFactory.CreateAnonymous(ctx));
        }
        await _next(ctx);
    }
}

public static class SessionMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxSessionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SessionMiddleware>();
    }
}
