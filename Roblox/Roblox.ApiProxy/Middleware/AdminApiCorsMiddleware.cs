using Microsoft.Extensions.Options;
using Roblox.ApiProxy.Configuration;
using Roblox.Web.Infrastructure.Middleware;

namespace Roblox.ApiProxy.Middleware;

public sealed class AdminApiCorsMiddleware
{
    private const string AdminHost = "admin.averia.lol";
    private readonly AdminApiOptions _options;
    private readonly RequestDelegate _next;

    public AdminApiCorsMiddleware(RequestDelegate next, IOptions<AdminApiOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsAdminHost(context.Request.Host.Host))
        {
            await _next(context);
            return;
        }

        ApplyCorsHeaders(context);

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            RobloxCsrfMiddleware.EnsureTokenHeader(context);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/v1"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }

    private void ApplyCorsHeaders(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin) ||
            !_options.CorsAllowedOrigins.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        context.Response.Headers.AccessControlAllowOrigin = origin;
        context.Response.Headers.AccessControlAllowCredentials = "true";
        context.Response.Headers.AccessControlAllowHeaders = "x-csrf-token, content-type";
        context.Response.Headers.AccessControlAllowMethods = "GET, POST, PUT, PATCH, DELETE, OPTIONS";
        context.Response.Headers.AccessControlExposeHeaders = "x-csrf-token";
        context.Response.Headers.Vary = "Origin";
    }

    private static bool IsAdminHost(string? host)
    {
        return string.Equals(host, AdminHost, StringComparison.OrdinalIgnoreCase);
    }
}
