using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Web.Infrastructure.Middleware;

public class ProxyForwardedAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RobloxWebInfrastructureOptions _options;

    public ProxyForwardedAuthMiddleware(RequestDelegate next, IOptions<RobloxWebInfrastructureOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IRobloxRequestContextAccessor requestContextAccessor)
    {
        var endpoint = context.GetEndpoint();
        var isAuthorized = IsAuthorized(context);
        var requestContext = RobloxRequestContextFactory.CreateFromForwardedHeaders(context, isAuthorized, _options.RccAuthorization);
        requestContextAccessor.SetCurrent(requestContext);

        if (!SatisfiesEndpointRequirements(endpoint, requestContext, isAuthorized))
        {
            await SendUnauthorized(context);
            return;
        }

        await _next(context);
    }

    private static bool SatisfiesEndpointRequirements(Endpoint? endpoint, RobloxRequestContext requestContext, bool isAuthorized)
    {
        if (endpoint.IsInternalServiceOnly() && !isAuthorized)
        {
            return false;
        }

        if (endpoint.RequiresRobloxSession() && !requestContext.IsAuthenticated)
        {
            return false;
        }

        if (endpoint.RequiresRccRequest() && !requestContext.IsRcc)
        {
            return false;
        }

        if (endpoint.RequiresRobloxClient() && !requestContext.IsRobloxClient)
        {
            return false;
        }

        return true;
    }

    private static async Task SendUnauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            errors = new[]
            {
                new { code = 0, message = "Unauthorized (PRX)" },
            },
        });
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.Authorization))
        {
            return false;
        }

        return context.Request.Headers.TryGetValue(RobloxWebContextConstants.ProxyAuthorizationHeaderName, out var provided) &&
               provided.ToString() == _options.Authorization;
    }
}
