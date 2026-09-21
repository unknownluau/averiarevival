using System.Reflection;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Website.Controllers;

namespace Roblox.Web.Infrastructure.Tests;

public class GameLaunchRouteMetadataTests
{
    [Theory]
    [InlineData(nameof(WebController.GetJoinScript))]
    [InlineData(nameof(WebController.GetJoinScriptFromJobId))]
    public void JoinScriptRoutes_RequireSessionAndCsrf(string methodName)
    {
        var method = typeof(WebController).GetMethod(methodName)!;
        Assert.NotNull(method.GetCustomAttribute<RequireRobloxSessionAttribute>());
        Assert.NotNull(method.GetCustomAttribute<RequireRobloxCsrfAttribute>());
    }

    [Fact]
    public void NegotiateRoutes_RequireRobloxClient()
    {
        var method = typeof(BypassController).GetMethod(nameof(BypassController.Negotiate))!;
        Assert.NotNull(method.GetCustomAttribute<RequireRobloxClientAttribute>());
    }
}
