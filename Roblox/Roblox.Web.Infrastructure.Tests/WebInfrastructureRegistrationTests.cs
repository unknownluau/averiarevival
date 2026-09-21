using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Roblox.Services;
using Roblox.Web.Infrastructure.Configuration;
using Roblox.Web.Infrastructure.Extensions;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Services;

namespace Roblox.Web.Infrastructure.Tests;

public class WebInfrastructureRegistrationTests
{
    [Fact]
    public void AddRobloxWebInfrastructure_BindsOptionsAndRegistersCoreServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization"] = TestConstants.ProxyAuthorization,
                ["RccAuthorization"] = TestConstants.RccAuthorization,
                ["Jwt:Sessions"] = TestConstants.SessionJwtKey,
                ["InternalServiceHosts:0"] = "avatar.internal",
                ["InternalServiceRoutes:0:Hosts:0"] = "apisite.local",
                ["InternalServiceRoutes:0:PathPrefixes:0"] = "/avatar",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddRobloxWebInfrastructure(configuration);

        using var root = services.BuildServiceProvider();
        var options = root.GetRequiredService<IOptions<RobloxWebInfrastructureOptions>>().Value;

        Assert.Equal(TestConstants.ProxyAuthorization, options.Authorization);
        Assert.Equal(TestConstants.RccAuthorization, options.RccAuthorization);
        Assert.Equal(TestConstants.SessionJwtKey, options.SessionJwtKey);
        Assert.Contains("avatar.internal", options.InternalServiceHosts);
        Assert.Single(options.InternalServiceRoutes);
        Assert.Equal("apisite.local", options.InternalServiceRoutes[0].Hosts[0]);
        Assert.Equal("/avatar", options.InternalServiceRoutes[0].PathPrefixes[0]);
        Assert.NotNull(root.GetRequiredService<FileContentCache>());
        Assert.NotNull(root.GetRequiredService<IRobloxRequestContextAccessor>());

        using var scope = root.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RobloxServiceAccessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<UsersService>());
    }
}
