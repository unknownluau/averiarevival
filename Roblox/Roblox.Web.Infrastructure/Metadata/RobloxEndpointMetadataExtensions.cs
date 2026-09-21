using Microsoft.AspNetCore.Http;

namespace Roblox.Web.Infrastructure.Metadata;

public static class RobloxEndpointMetadataExtensions
{
    public static bool HasRobloxMetadata<T>(this Endpoint? endpoint) where T : Attribute
    {
        return endpoint?.Metadata.GetMetadata<T>() != null;
    }

    public static bool AllowsRobloxAnonymous(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<AllowRobloxAnonymousAttribute>();
    }

    public static bool RequiresRobloxSession(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<RequireRobloxSessionAttribute>();
    }

    public static bool RequiresRobloxCsrf(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<RequireRobloxCsrfAttribute>();
    }

    public static bool RequiresRccRequest(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<RequireRccRequestAttribute>();
    }

    public static bool RequiresRobloxClient(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<RequireRobloxClientAttribute>();
    }

    public static bool IsInternalServiceOnly(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<InternalServiceOnlyAttribute>();
    }

    public static bool ShouldSkipRobloxCsrf(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<SkipRobloxCsrfAttribute>();
    }

    public static bool IsBrowserFacingEndpoint(this Endpoint? endpoint)
    {
        return endpoint.HasRobloxMetadata<BrowserFacingEndpointAttribute>();
    }

    public static bool HasExplicitRobloxRequestRequirement(this Endpoint? endpoint)
    {
        return endpoint.IsInternalServiceOnly() ||
               endpoint.RequiresRobloxSession() ||
               endpoint.RequiresRobloxCsrf() ||
               endpoint.RequiresRccRequest() ||
               endpoint.RequiresRobloxClient();
    }
}
