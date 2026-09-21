using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Roblox.Web.Infrastructure.Http;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Web.Infrastructure.Middleware;

public sealed class RobloxCsrfMiddleware
{
    public const string HeaderName = "x-csrf-token";
    private readonly RequestDelegate _next;

    public RobloxCsrfMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null ||
            !endpoint.RequiresRobloxCsrf() ||
            endpoint.ShouldSkipRobloxCsrf() ||
            HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (HttpMethods.IsOptions(context.Request.Method))
        {
            EnsureTokenHeader(context);
            await _next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(RobloxWebContextConstants.CsrfCookieName, out var cookieValue) ||
            string.IsNullOrWhiteSpace(cookieValue))
        {
            await SendTokenFailureAsync(context, CreateAndSetToken(context), setCookie: true);
            return;
        }

        var provided = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!TokenEquals(cookieValue, provided))
        {
            await SendTokenFailureAsync(context, cookieValue, setCookie: false);
            return;
        }

        await _next(context);
    }

    public static string EnsureTokenHeader(HttpContext context)
    {
        var token = context.Request.Cookies.TryGetValue(RobloxWebContextConstants.CsrfCookieName, out var cookieValue) &&
                    !string.IsNullOrWhiteSpace(cookieValue)
            ? cookieValue
            : CreateAndSetToken(context);

        context.Response.Headers[HeaderName] = token;
        return token;
    }

    private static bool TokenEquals(string expected, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided))
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string CreateAndSetToken(HttpContext context)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        context.Response.Cookies.Append(RobloxWebContextConstants.CsrfCookieName, token, new CookieOptions
        {
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            MaxAge = TimeSpan.FromMinutes(20),
        });
        return token;
    }

    private static async Task SendTokenFailureAsync(HttpContext context, string token, bool setCookie)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.Headers[HeaderName] = token;
        if (!setCookie)
        {
            context.Response.Cookies.Append(RobloxWebContextConstants.CsrfCookieName, token, new CookieOptions
            {
                SameSite = SameSiteMode.Lax,
                Path = "/",
                IsEssential = true,
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                MaxAge = TimeSpan.FromMinutes(20),
            });
        }

        await context.Response.WriteAsJsonAsync(new
        {
            errors = new[]
            {
                new
                {
                    code = 0,
                    message = "Token Validation Failed",
                },
            },
        });
    }
}
