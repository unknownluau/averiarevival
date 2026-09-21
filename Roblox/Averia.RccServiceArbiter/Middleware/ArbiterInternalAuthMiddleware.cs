using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;

namespace Averia.RccServiceArbiter.Middleware;

public sealed class ArbiterInternalAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ArbiterInternalAuthMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint.IsInternalServiceOnly() && !IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                errors = new[]
                {
                    new { code = 0, message = "Unauthorized" },
                },
            });
            return;
        }

        await _next(context);
    }

    private bool IsAuthorized(HttpContext context)
    {
        var expected = _configuration["Authorization"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return context.Request.Headers.TryGetValue(RobloxWebContextConstants.ProxyAuthorizationHeaderName, out var provided) &&
               provided.ToString() == expected;
    }
}
