using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Roblox.ApiProxy.Configuration;
using Roblox.Models.Users;
using Roblox.Web.Infrastructure.Http;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Roblox.ApiProxy.Middleware;

public sealed class FrontendProxyMiddleware
{
    private const string NoCache = "public,max-age=0,no-cache,must-revalidate";
    private const string NextStaticCache = "public,max-age=2592000";

    private static readonly HttpMessageInvoker HttpClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        EnableMultipleHttp2Connections = true,
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        UseCookies = false,
        UseProxy = false,
    });

    private static readonly ForwarderRequestConfig RequestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromSeconds(100),
    };

    // These paths are still handled by Roblox.Website on the public website hosts.
    // Specific YARP routes also bypass this middleware before this list is checked.
    private static readonly string[] BackendPathPrefixes =
    [
        "/v1",
        "/v2",
        "/v3",
        "/api/",
        "/apisite/",
        "/swagger/",
        "/health/",
        "/gs/",
        "/moderation/",
        "/admin-api/",
        "/donation-api/",
        "/stripe-api/",
        "/auth/",
        "/ide/",
        "/internal/",
        "/css/",
        "/js/",
        "/img/",
        "/images/",
        "/unsecuredcontent/",
        "/asset",
        "/assets/",
        "/thumbs/",
        "/thumbnail/",
        "/avatar-thumbnail/",
        "/avatar-thumbnail-3d/",
        "/headshot-thumbnail/",
        "/icons/",
        "/game/",
        "/data/",
        "/rcc/",
        "/persistence/",
        "/presence/",
        "/device/",
        "/login/",
        "/oauth/",
        "/.well-known/",
        "/notifications/",
        "/marketplace/",
        "/mobile/",
        "/mobile-ads/",
        "/mobileapi/",
        "/toolbox-service/",
        "/universal-app-configuration/",
        "/teamtest/",
        "/setting/",
        "/user/",
        "/universes/",
        "/abusereport/",
        "/alerts/",
        "/badges/",
        "/bot/",
        "/botapi/",
        "/buildersclub/",
        "/client/",
        "/currency/",
        "/developerproducts/",
        "/gametransactions/",
        "/ownership/",
        "/studio/",
        "/feeds/getuserfeed",
        "/membership/notapproved.aspx",
        "/users/filter-friends",
        "/users/account-info",
        "/users/getbanstatus.ashx",
        "/users/get-by-username",
        "/users/favorites/list-json",
        "/users/inventory/list-json",
        "/users/liststaff.ashx",
        "/users/profile/robloxcollections-json",
        "/users/set-builders-club",
        "/user-sponsorship/",
        "/userads/redirect",
        "/usercheck/show-tos",
        "/comments/get-json",
        "/comments/post",
        "/search/users/results",
        "/develop/upload",
        "/friends/filter",
        "/incoming-items/counts",
        "/info/blog",
        "/joinserver",
        "/my/balance",
        "/my/economy-status",
        "/my/friendsonline",
        "/my/places.aspx",
        "/my/settings/json",
        "/pe",
        "/set-year",
        "/sign-out/",
        "/sponsoredpage/",
        "/download3",
        "/generate",
        "/genereate",
        "/getallowed",
        "/getcurrentclientversionupload",
        "/groups/search/lookup",
        "/games/getgameinstancesjson",
        "/placelauncher.ashx",
        "/version",
        "/chat",
    ];

    private static readonly string[][] BackendPathPrefixesByFirstCharacter = CreatePrefixBuckets();

    private readonly string _destinationPrefix;
    private readonly IHttpForwarder _forwarder;
    private readonly ILogger<FrontendProxyMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _publicHosts;

    public FrontendProxyMiddleware(
        RequestDelegate next,
        IHttpForwarder forwarder,
        IOptions<FrontendProxyOptions> options,
        ILogger<FrontendProxyMiddleware> logger)
    {
        _next = next;
        _forwarder = forwarder;
        _logger = logger;
        _destinationPrefix = NormalizeDestinationPrefix(options.Value.DestinationPrefix);
        _publicHosts = NormalizePublicHosts(options.Value.PublicHosts);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldProxyToFrontend(context))
        {
            await _next(context);
            return;
        }

        if (!CanUseFrontend(context))
        {
            return;
        }

        context.Response.OnStarting(UpdateCacheHeaders, context);
        var error = await _forwarder.SendAsync(
            context,
            _destinationPrefix,
            HttpClient,
            RequestConfig,
            HttpTransformer.Default,
            context.RequestAborted);

        if (error != ForwarderError.None)
        {
            _logger.LogWarning("Frontend proxy request failed with {ForwarderError}", error);
        }
    }

    private static bool CanUseFrontend(HttpContext context)
    {
        if (IsFrontendPublicAsset(context.Request.Path.Value ?? string.Empty))
        {
            return true;
        }

        var session = context.GetRobloxRequestContext()?.Session;
        if (session == null)
        {
            context.Response.Redirect("/");
            return false;
        }

        if (session.accountStatus is AccountStatus.Suppressed or AccountStatus.Poisoned or AccountStatus.Deleted)
        {
            context.Response.Redirect("/auth/notapproved");
            return false;
        }

        return true;
    }

    private static Task UpdateCacheHeaders(object state)
    {
        var context = (HttpContext)state;
        if (context.Response.StatusCode == StatusCodes.Status404NotFound ||
            context.Response.StatusCode is >= 500 and <= 599)
        {
            context.Response.Headers[HeaderNames.CacheControl] = NoCache;
        }
        else if (context.Response.StatusCode == StatusCodes.Status200OK &&
                 context.Request.Path.StartsWithSegments("/_next/static"))
        {
            context.Response.Headers[HeaderNames.CacheControl] = NextStaticCache;
        }

        return Task.CompletedTask;
    }

    private bool ShouldProxyToFrontend(HttpContext context)
    {
        var host = context.Request.Host.Host;
        if (!_publicHosts.Contains(host))
        {
            return false;
        }

        var reverseProxyRoute = context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>();
        if (reverseProxyRoute != null && !IsCatchAll(reverseProxyRoute.Config.Match.Path))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        return !IsBackendOwnedPath(path);
    }

    private static bool IsBackendOwnedPath(string path)
    {
        if (path.Length == 0 || path == "/")
        {
            return true;
        }

        if (IsFrontendOwnedPath(path))
        {
            return false;
        }

        if (path.Contains("/canmanage/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("filter-friends", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("multiget-friend-requests", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("abusereport", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/universes/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/unsecuredcontent/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
            IsNumericPath(path, "/places/", "/settings") ||
            IsNumericPath(path, "/users/", string.Empty))
        {
            return true;
        }

        var bucketIndex = char.ToLowerInvariant(path[1]);
        if (bucketIndex >= BackendPathPrefixesByFirstCharacter.Length)
        {
            return false;
        }

        foreach (var prefix in BackendPathPrefixesByFirstCharacter[bucketIndex])
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFrontendOwnedPath(string path)
    {
        return IsFrontendPublicAsset(path);
    }

    private static bool IsFrontendPublicAsset(string path)
    {
        if (path.StartsWith("/_next/static/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/period-styles/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/theme-assets/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Equals("/js/bootstrap.min.css", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/js/axios.min.js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/3d/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js/roblox/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith("/img/", StringComparison.OrdinalIgnoreCase) &&
               (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
    }

    private static string[][] CreatePrefixBuckets()
    {
        var buckets = new string[128][];
        for (var index = 0; index < buckets.Length; index++)
        {
            buckets[index] = [];
        }

        foreach (var group in BackendPathPrefixes.GroupBy(prefix => char.ToLowerInvariant(prefix[1])))
        {
            buckets[group.Key] = group.ToArray();
        }

        return buckets;
    }

    private static bool IsCatchAll(string? routePattern)
    {
        return routePattern is null or "/{**catch-all}" or "{**catch-all}";
    }

    private static bool IsNumericPath(string path, string prefix, string suffix)
    {
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var valueLength = path.Length - prefix.Length - suffix.Length;
        if (valueLength <= 0)
        {
            return false;
        }

        var value = path.AsSpan(prefix.Length, valueLength);
        foreach (var character in value)
        {
            if (character is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeDestinationPrefix(string destinationPrefix)
    {
        if (!Uri.TryCreate(destinationPrefix, UriKind.Absolute, out var destination) ||
            destination.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"{FrontendProxyOptions.SectionName}:{nameof(FrontendProxyOptions.DestinationPrefix)} must be an absolute HTTP or HTTPS URL.");
        }

        return destinationPrefix.EndsWith('/') ? destinationPrefix : destinationPrefix + '/';
    }

    private static HashSet<string> NormalizePublicHosts(IEnumerable<string> publicHosts)
    {
        var hosts = publicHosts
            .Where(host => !string.IsNullOrWhiteSpace(host))
            .Select(host => host.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (hosts.Count == 0)
        {
            throw new InvalidOperationException(
                $"{FrontendProxyOptions.SectionName}:{nameof(FrontendProxyOptions.PublicHosts)} must contain at least one host.");
        }

        return hosts;
    }
}
