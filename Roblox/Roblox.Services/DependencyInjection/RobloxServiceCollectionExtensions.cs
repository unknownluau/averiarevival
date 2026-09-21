using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Roblox.Services.Caching;

namespace Roblox.Services.DependencyInjection;

public static class RobloxServiceCollectionExtensions
{
    public static IServiceCollection AddRobloxServiceLayer(this IServiceCollection services)
    {
        foreach (var serviceType in typeof(ServiceBase).Assembly.GetTypes().Where(IsRegistrableServiceType))
        {
            if (ShouldRegisterAsSingleton(serviceType))
            {
                services.TryAddSingleton(serviceType);
                continue;
            }

            services.TryAddTransient(serviceType);
        }

        return services;
    }

    private static bool IsRegistrableServiceType(Type type)
    {
        return type.IsClass &&
               !type.IsAbstract &&
               !type.ContainsGenericParameters &&
               type != typeof(ServiceBase) &&
               type.IsSubclassOf(typeof(ServiceBase));
    }

    private static bool ShouldRegisterAsSingleton(Type serviceType)
    {
        return serviceType == typeof(ChatService) ||
               serviceType == typeof(FilterService) ||
               serviceType == typeof(PromocodesService) ||
               serviceType == typeof(RobloxAssetService) ||
               serviceType == typeof(PlayerSecurityService) ||
               serviceType == typeof(ApplicationService) ||
               serviceType == typeof(R2StorageService) ||
               serviceType == typeof(AvatarCache) ||
               serviceType == typeof(DistributedJsonCache) ||
               InheritsGeneric(serviceType, typeof(global::Roblox.Services.GenericMemoryCache<,>));
    }

    private static bool InheritsGeneric(Type candidate, Type genericBase)
    {
        for (var current = candidate; current != null && current != typeof(object); current = current.BaseType!)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBase)
            {
                return true;
            }
        }

        return false;
    }
}
