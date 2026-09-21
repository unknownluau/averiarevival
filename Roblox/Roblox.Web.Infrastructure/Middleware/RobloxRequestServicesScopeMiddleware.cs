using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Roblox.Web.Infrastructure.Middleware;

public sealed class RobloxRequestServicesScopeMiddleware
{
    private readonly RequestDelegate _next;

    public RobloxRequestServicesScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var _ = Roblox.Services.ServiceProvider.BeginScope(context.RequestServices);
        await _next(context);
    }
}

public static class RobloxRequestServicesScopeApplicationBuilderExtensions
{
    public static IApplicationBuilder UseRobloxRequestServicesScope(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RobloxRequestServicesScopeMiddleware>();
    }
}
