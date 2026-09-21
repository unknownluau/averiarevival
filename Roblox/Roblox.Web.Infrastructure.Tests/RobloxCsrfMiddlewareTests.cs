using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Web.Infrastructure.Middleware;

namespace Roblox.Web.Infrastructure.Tests;

public class RobloxCsrfMiddlewareTests
{
    [Fact]
    public async Task ProtectedOptionsRequest_EmitsCsrfTokenHeaderAndContinues()
    {
        var nextCalled = false;
        var context = InfrastructureTestHelpers.Context(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()));
        context.Request.Method = HttpMethods.Options;
        context.Request.Path = "/v1/protected";

        var middleware = new RobloxCsrfMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers[RobloxCsrfMiddleware.HeaderName].ToString()));
        Assert.Contains(RobloxWebContextConstants.CsrfCookieName, context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task ProtectedOptionsRequest_WithExistingCookie_ExposesExistingToken()
    {
        const string csrf = "existing-token";
        var context = InfrastructureTestHelpers.Context(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()));
        context.Request.Method = HttpMethods.Options;
        context.Request.Path = "/v1/protected";
        InfrastructureTestHelpers.AddCookie(context, RobloxWebContextConstants.CsrfCookieName, csrf);

        var middleware = new RobloxCsrfMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(csrf, context.Response.Headers[RobloxCsrfMiddleware.HeaderName].ToString());
        Assert.DoesNotContain(RobloxWebContextConstants.CsrfCookieName, context.Response.Headers.SetCookie.ToString());
    }
}
