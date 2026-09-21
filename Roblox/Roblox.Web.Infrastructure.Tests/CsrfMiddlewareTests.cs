using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Metadata;
using Roblox.Website.Middleware;

namespace Roblox.Web.Infrastructure.Tests;

public class CsrfMiddlewareTests
{
    [Fact]
    public async Task UnmarkedUnsafeMethod_DoesNotRequireToken()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(),
            "POST");

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public async Task MarkedSafeMethods_DoNotRequireToken(string method)
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()),
            method);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task MarkedUnsafeMethod_MissingTokenFailsAndEmitsReplacementToken()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()),
            "POST");

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers["x-csrf-token"].ToString()));
        Assert.Contains(CsrfMiddleware.CookieName, context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task MarkedUnsafeMethod_MismatchedHeaderFailsWithExistingToken()
    {
        const string csrf = "known-csrf";
        var token = InfrastructureTestHelpers.CreateCsrfToken(csrf);

        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()),
            "POST",
            ctx =>
            {
                InfrastructureTestHelpers.AddCookie(ctx, CsrfMiddleware.CookieName, token);
                ctx.Request.Headers["x-csrf-token"] = "wrong";
            });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal(csrf, context.Response.Headers["x-csrf-token"].ToString());
    }

    [Fact]
    public async Task MarkedUnsafeMethod_InvalidCookieFailsAndEmitsReplacementToken()
    {
        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()),
            "POST",
            ctx =>
            {
                InfrastructureTestHelpers.AddCookie(ctx, CsrfMiddleware.CookieName, "not-a-jwt");
                ctx.Request.Headers["x-csrf-token"] = "anything";
            });

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(context.Response.Headers["x-csrf-token"].ToString()));
        Assert.Contains(CsrfMiddleware.CookieName, context.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task MarkedUnsafeMethod_ValidCookieAndHeaderAllowsRequest()
    {
        const string csrf = "valid-csrf";
        var token = InfrastructureTestHelpers.CreateCsrfToken(csrf);

        var (context, nextCalled) = await InfrastructureTestHelpers.InvokeCsrfAsync(
            InfrastructureTestHelpers.Endpoint(new RequireRobloxCsrfAttribute()),
            "POST",
            ctx =>
            {
                InfrastructureTestHelpers.AddCookie(ctx, CsrfMiddleware.CookieName, token);
                ctx.Request.Headers["x-csrf-token"] = csrf;
            });

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }
}
