using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Roblox.Web.Infrastructure.Configuration;

namespace Roblox.Web.Infrastructure.Http;

public class RobloxRequestContextAccessor : IRobloxRequestContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly RobloxWebInfrastructureOptions _options;

    public RobloxRequestContextAccessor(IHttpContextAccessor httpContextAccessor, IOptions<RobloxWebInfrastructureOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public RobloxRequestContext Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new RobloxRequestContext();
            }

            var existing = httpContext.GetRobloxRequestContext();
            if (existing != null)
            {
                return existing;
            }

            var created = RobloxRequestContextFactory.CreateAnonymous(httpContext, _options.RccAuthorization);
            httpContext.SetRobloxRequestContext(created);
            return created;
        }
    }

    public void SetCurrent(RobloxRequestContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        httpContext.SetRobloxRequestContext(context);
    }
}
