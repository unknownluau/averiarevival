using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Roblox.ApiProxy.Configuration;
using Roblox.Web.Infrastructure.Admin;

namespace Roblox.ApiProxy.Middleware;

public sealed class AdminFrontendMiddleware
{
    private const string NoCache = "public,max-age=0,no-cache,must-revalidate";
    private const string JavaScriptContentType = "application/javascript";
    private const string CssContentType = "text/css";
    private const string HtmlContentType = "text/html";
    private const string PngContentType = "image/png";

    private static readonly string CacheBust = Guid.NewGuid().ToString("N");

    private readonly AdminFrontendOptions _options;
    private readonly AdminApiOptions _adminApiOptions;
    private readonly IAdminSessionResolver _sessionResolver;
    private readonly IAdminStaffAuthorizationService _staffAuthorization;
    private readonly IAdminTwoFactorStore _twoFactorStore;
    private readonly ILogger<AdminFrontendMiddleware> _logger;
    private readonly RequestDelegate _next;

    public AdminFrontendMiddleware(
        RequestDelegate next,
        IOptions<AdminFrontendOptions> options,
        IOptions<AdminApiOptions> adminApiOptions,
        IAdminSessionResolver sessionResolver,
        IAdminStaffAuthorizationService staffAuthorization,
        IAdminTwoFactorStore twoFactorStore,
        ILogger<AdminFrontendMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _adminApiOptions = adminApiOptions.Value;
        _sessionResolver = sessionResolver;
        _staffAuthorization = staffAuthorization;
        _twoFactorStore = twoFactorStore;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.Value is "/admin")
        {
            context.Response.Redirect("/admin/");
            return;
        }

        var session = await _sessionResolver.TryResolveAsync(context);
        if (session == null || !await _staffAuthorization.IsStaffAsync(session.userId))
        {
            context.Response.Redirect("/home");
            return;
        }

        if (!await _twoFactorStore.IsVerifiedAsync(session.userId, session.sessionId))
        {
            context.Response.Redirect(GetTwoFactorPromptUrl(context));
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        context.Response.Headers[HeaderNames.CacheControl] = NoCache;

        if (path.Equals("/admin/build-redirect/bundle.js", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/admin/build/" + CacheBust + "/bundle.js");
            return;
        }

        if (path.Equals("/admin/build-redirect/bundle.css", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/admin/build/" + CacheBust + "/bundle.css");
            return;
        }

        if (TryGetVersionedBundle(path, out var bundleFileName, out var contentType))
        {
            await SendAdminFileAsync(context, Path.Combine("build", bundleFileName), contentType);
            return;
        }

        if (path.StartsWith("/admin/build/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (path.Equals("/admin/favicon.png", StringComparison.OrdinalIgnoreCase))
        {
            await SendAdminFileAsync(context, "favicon.png", PngContentType);
            return;
        }

        await SendAdminFileAsync(context, "index.html", HtmlContentType);
    }

    private async Task SendAdminFileAsync(HttpContext context, string relativePath, string contentType)
    {
        var rootDirectory = ResolveRootDirectory();
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            _logger.LogWarning("Admin frontend root directory is not configured.");
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var rootFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootDirectory));
        var fileFullPath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!fileFullPath.StartsWith(rootFullPath + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !string.Equals(fileFullPath, rootFullPath, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!File.Exists(fileFullPath))
        {
            _logger.LogWarning("Admin frontend file was not found: {FilePath}", fileFullPath);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = contentType;
        await context.Response.SendFileAsync(fileFullPath);
    }

    private string ResolveRootDirectory()
    {
        return string.IsNullOrWhiteSpace(_options.RootDirectory)
            ? Roblox.Configuration.AdminBundleDirectory
            : _options.RootDirectory;
    }

    private static bool TryGetVersionedBundle(string path, out string fileName, out string contentType)
    {
        const string prefix = "/admin/build/";
        fileName = string.Empty;
        contentType = string.Empty;

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.EndsWith("/bundle.js", StringComparison.OrdinalIgnoreCase) &&
            path.Length > prefix.Length + "/bundle.js".Length)
        {
            fileName = "bundle.js";
            contentType = JavaScriptContentType;
            return true;
        }

        if (path.EndsWith("/bundle.css", StringComparison.OrdinalIgnoreCase) &&
            path.Length > prefix.Length + "/bundle.css".Length)
        {
            fileName = "bundle.css";
            contentType = CssContentType;
            return true;
        }

        return false;
    }

    private static string GetReturnUrl(HttpContext context)
    {
        return context.Request.PathBase + context.Request.Path + context.Request.QueryString;
    }

    private string GetTwoFactorPromptUrl(HttpContext context)
    {
        return _adminApiOptions.PublicBaseUrl.TrimEnd('/') +
               "/2fa?returnUrl=" +
               Uri.EscapeDataString(GetReturnUrl(context));
    }
}
