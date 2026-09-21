using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Roblox.Services;
using Roblox.Services.DependencyInjection;
using Roblox.Web.Infrastructure.Middleware;

namespace Roblox.Web.Infrastructure.Tests;

public class RequestServicesScopeMiddlewareTests
{
    [Fact]
    public async Task UsesHttpContextRequestServicesForServiceProviderScope()
    {
        var services = new ServiceCollection();
        services.AddRobloxServiceLayer();
        services.AddScoped<ScopedCarrier>();
        using var root = services.BuildServiceProvider();
        Roblox.Services.ServiceProvider.Initialize(root);

        using var requestScope = root.CreateScope();
        var expected = requestScope.ServiceProvider.GetRequiredService<ScopedCarrier>();
        var context = InfrastructureTestHelpers.Context();
        context.RequestServices = requestScope.ServiceProvider;
        ScopedCarrier? resolved = null;

        var middleware = new RobloxRequestServicesScopeMiddleware(_ =>
        {
            using var service = Roblox.Services.ServiceProvider.GetOrCreate<ScopedCarrierService>();
            resolved = service.Carrier;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.Same(expected, resolved);
    }

    private sealed class ScopedCarrier
    {
    }

    private sealed class ScopedCarrierService : ServiceBase
    {
        public ScopedCarrierService(ScopedCarrier carrier)
        {
            Carrier = carrier;
        }

        public ScopedCarrier Carrier { get; }
    }
}
