using Roblox.Web.Infrastructure.Http;

namespace Roblox.Website.Middleware;

public class RobloxPlayerCorsMiddleware
{
    private RequestDelegate _next;
    public RobloxPlayerCorsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private string GenerateCspHeader(bool isAuthenticated)
    {
        var connectSrc = "'self' https://*.averia.lol wss://*.averia.lol https://cdn.jsdelivr.net https://hcaptcha.com https://*.hcaptcha.com https://*.cdn.com https://challenges.cloudflare.com ws://localhost:*";

        var imgSrc = "'self' data: https://cdn.discordapp.com";
        if (isAuthenticated)
        {
            imgSrc += " https://*.averia.lol https://*.cdn.com https://challenges.cloudflare.com";
        }

        var mediaSrc = "'self' https://*.averia.lol";

        var scriptSrc =
            "'self' 'unsafe-inline' 'unsafe-eval' " + 
            "https://challenges.cloudflare.com " +
            "https://*.cloudflare.com " +
            "https://*.cloudflareinsights.com " +
            "https://hcaptcha.com " +
            "https://*.hcaptcha.com " +
            "https://cdn.jsdelivr.net " +
            "https://averia.lol " +
            "https://*.averia.lol " +
            "http://js.rbxcdn.com";

        scriptSrc += " https://cdn.jsdelivr.net/npm/cryptocoins-icons@2.9.0/gulpfile.min.js";

        var fontSrc = "'self' https://fonts.gstatic.com https://cdn.jsdelivr.net http://www.averia.lol https://averia.lol https://*.averia.lol https://www.averia.lol/fonts/GothamSSmBold.woff2 https://www.averia.lol/fonts/GothamSSmMedium.woff2 https://www.averia.lol/fonts/GothamSSmBook.woff2";

        var styleSrc = "";

#if DEBUG
        styleSrc = $" {Configuration.BaseUrl}/fonts/gotham1.css {Configuration.BaseUrl}/fonts/gotham1.css";
        fontSrc += $" {Configuration.BaseUrl}/fonts/GothamSSmBold.woff2 {Configuration.BaseUrl}/fonts/GothamSSmMedium.woff2 {Configuration.BaseUrl}/fonts/GothamSSmBook.woff2 {Configuration.BaseUrl}/fonts/GothamSSmLight.woff2 {Configuration.BaseUrl}/fonts/GothamSSmBlack.woff2";
        imgSrc += " https://*.averia.lol";
#endif

        styleSrc += " https://cdn.jsdelivr.net/npm/cryptocoins-icons@2.9.0/webfont/cryptocoins.min.css";

        return "default-src 'self'; media-src " + mediaSrc + "; img-src " + imgSrc + "; child-src 'self'; script-src " + scriptSrc + "; frame-src 'self' https://hcaptcha.com https://*.hcaptcha.com https://challenges.cloudflare.com http://challenges.cloudflare.com; style-src 'unsafe-inline' 'self' https://fonts.googleapis.com https://hcaptcha.com https://*.hcaptcha.com https://averia.lol https://www.averia.lol https://cdn.jsdelivr.net/npm/bootstrap-icons/font/bootstrap-icons.css https://cdn.jsdelivr.net/gh/AllienWorks/cryptocoins@2.7.0/webfont/cryptocoins.css https://cdn.jsdelivr.net/npm/bootstrap@5.1.3/dist/css/bootstrap.min.css https://averia.lol/fonts/gotham1.css http://*.averia.lol" + styleSrc + "; font-src " + fontSrc + "; connect-src " + connectSrc + "; worker-src 'self';";
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var isAuthenticated = (ctx.GetRobloxRequestContext() ?? RobloxRequestContextFactory.CreateAnonymous(ctx)).IsAuthenticated;
        ctx.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        ctx.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
        ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        ctx.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        ctx.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Content-Security-Policy"] = GenerateCspHeader(isAuthenticated);
        await _next(ctx);
    }
}

public static class RobloxPlayerCorsMiddlewareExtensions
{
    public static IApplicationBuilder UseRobloxPlayerCorsMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RobloxPlayerCorsMiddleware>();
    }
}
