using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;

namespace Roblox.Web.Infrastructure.Middleware;

public class ApiProxyForwardedAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RobloxWebInfrastructureOptions _options;

    public ApiProxyForwardedAuthMiddleware(RequestDelegate next, IOptions<RobloxWebInfrastructureOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, IRobloxRequestContextAccessor requestContextAccessor)
    {
        var incomingIsTrusted = IsAuthorized(context);

        var requestContext = incomingIsTrusted
            ? RobloxRequestContextFactory.CreateFromForwardedHeaders(context, true, _options.RccAuthorization)
            : await CreateBrowserRequestContextAsync(context);

        if (!incomingIsTrusted)
        {
            ClearForwardedHeaders(context);
        }

        var shouldDecorate = ShouldDecorateRequest(context.Request.Host.Host, context.Request.Path);

        if (!shouldDecorate)
        {
            requestContextAccessor.SetCurrent(requestContext);
            await _next(context);
            return;
        }

        requestContext.IsTrustedInternalRequest = true;
        requestContextAccessor.SetCurrent(requestContext);

        if (!string.IsNullOrWhiteSpace(_options.Authorization))
        {
            context.Request.Headers[RobloxWebContextConstants.ProxyAuthorizationHeaderName] = _options.Authorization;
        }

        context.Request.Headers[RobloxWebContextConstants.ClientIpHashHeaderName] = requestContext.HashedIp;
        context.Request.Headers[RobloxWebContextConstants.UserAgentHeaderName] = requestContext.UserAgent;
        context.Request.Headers[RobloxWebContextConstants.GameIdHeaderName] = requestContext.CurrentGameId;
        context.Request.Headers[RobloxWebContextConstants.PlaceIdHeaderName] = requestContext.CurrentPlaceId.ToString();
        context.Request.Headers[RobloxWebContextConstants.AuthTypeHeaderName] = requestContext.IsRcc
            ? "rcc"
            : requestContext.IsRobloxClient ? "roblox" : "browser";

        if (requestContext.Session != null)
        {
            context.Request.Headers[RobloxWebContextConstants.UserIdHeaderName] = requestContext.Session.userId.ToString();
            context.Request.Headers[RobloxWebContextConstants.UsernameHeaderName] = requestContext.Session.username;
            context.Request.Headers[RobloxWebContextConstants.SessionIdHeaderName] = requestContext.Session.sessionId;
            context.Request.Headers[RobloxWebContextConstants.AccountStatusHeaderName] = requestContext.Session.accountStatus.ToString();
        }
        
        await _next(context);
    }

    private async Task<RobloxRequestContext> CreateBrowserRequestContextAsync(HttpContext context)
    {
        var requestContext = RobloxRequestContextFactory.CreateAnonymous(context, _options.RccAuthorization);
        var resolvedSession = await RobloxSessionResolver.TryResolveFromCookie(context);
        return resolvedSession == null
            ? requestContext
            : RobloxRequestContextFactory.CreateWithSession(context, resolvedSession.Session, _options.RccAuthorization);
    }

    private static void ClearForwardedHeaders(HttpContext context)
    {
        context.Request.Headers.Remove(RobloxWebContextConstants.ProxyAuthorizationHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.UserIdHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.UsernameHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.SessionIdHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.AccountStatusHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.AuthTypeHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.GameIdHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.PlaceIdHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.ClientIpHashHeaderName);
        context.Request.Headers.Remove(RobloxWebContextConstants.UserAgentHeaderName);
    }

    private bool ShouldDecorateRequest(string host, PathString path)
    {
        if (_options.InternalServiceRoutes.Any(route => RouteMatches(route, host, path)))
        {
            return true;
        }

        return _options.InternalServiceHosts.Any(candidate => HostMatches(candidate, host));
    }

    private static bool RouteMatches(RobloxInternalServiceRoute route, string host, PathString path)
    {
        var hostMatches = route.Hosts.Count == 0 || route.Hosts.Any(candidate => HostMatches(candidate, host));
        if (!hostMatches)
        {
            return false;
        }

        if (route.PathPrefixes.Count == 0)
        {
            return true;
        }

        return route.PathPrefixes.Any(prefix => path.StartsWithSegments(NormalizePathPrefix(prefix), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePathPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "/";
        }

        if (!prefix.StartsWith('/'))
        {
            prefix = "/" + prefix;
        }

        return prefix.Length > 1 ? prefix.TrimEnd('/') : prefix;
    }

    private static bool HostMatches(string candidate, string host)
    {
        if (string.Equals(candidate, host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!candidate.StartsWith('*'))
        {
            return false;
        }

        var suffix = candidate[1..];
        return host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
               host.Length > suffix.Length;
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
