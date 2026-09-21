using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roblox.Web.Infrastructure.Auth;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;
using Roblox.Services.DependencyInjection;

namespace Roblox.Web.Infrastructure.Extensions;

public static class RobloxWebInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRobloxWebInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRobloxServiceLayer();
        services.AddHttpContextAccessor();
        services.TryAddSingleton<FileContentCache>();
        services.TryAddScoped<RobloxServiceAccessor>();
        services.AddSingleton<IRobloxRequestContextAccessor, RobloxRequestContextAccessor>();
        services.AddOptions<RobloxWebInfrastructureOptions>().Configure(options =>
        {
            options.Authorization = configuration["Authorization"];
            options.RccAuthorization = configuration["RccAuthorization"];
            options.SessionJwtKey = configuration["Jwt:Sessions"];
            options.InternalServiceHosts = configuration.GetSection("InternalServiceHosts").Get<List<string>>() ?? new List<string>();
            options.InternalServiceRoutes = configuration.GetSection("InternalServiceRoutes").Get<List<RobloxInternalServiceRoute>>() ?? new List<RobloxInternalServiceRoute>();
        });

        var rccAuthorization = configuration["RccAuthorization"];
        if (!string.IsNullOrWhiteSpace(rccAuthorization))
        {
            Roblox.Configuration.RccAuthorization = rccAuthorization;
        }

        var sessionJwtKey = configuration["Jwt:Sessions"];
        if (!string.IsNullOrWhiteSpace(sessionJwtKey))
        {
            RobloxSessionTokenCodec.Configure(sessionJwtKey);
        }

        return services;
    }
}
