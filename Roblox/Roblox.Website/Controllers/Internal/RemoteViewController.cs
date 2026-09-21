using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Text.Json;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Website.Controllers;

[ApiController]
[InternalServiceOnly]
[Route("/")]
public sealed class RemoteViewController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, Func<IEnumerable<dynamic>, Task<string>>> _viewHandlers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient _frontendClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static string _frontendBaseUrl = string.Empty;

    public static void Configure(string frontendBaseUrl)
    {
        _frontendBaseUrl = frontendBaseUrl?.TrimEnd('/') ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(_frontendBaseUrl))
        {
            _frontendClient.BaseAddress = new Uri(_frontendBaseUrl + "/");
        }
    }

    static RemoteViewController()
    {
        RegisterViewHandlers();
    }

    private static void RegisterViewHandlers()
    {
        _viewHandlers["dashboard"] = args => RenderView("/home", args);
        _viewHandlers["trades"] = args => RenderView("/trades", args);
        _viewHandlers["tradeWithUser"] = args =>
        {
            var userId = ExtractUserId(args);
            return RenderView(userId.HasValue ? $"/users/{userId.Value}/trade" : "/trades", args);
        };
        _viewHandlers["groupSearch"] = args => RenderView("/search/groups", args);
        _viewHandlers["userSearch"] = args => RenderView("/search/users", args);
        _viewHandlers["groupCreate"] = args => RenderView("/groups/create", args);
        _viewHandlers["groupConfigure"] = args => RenderView("/groups/configure", args);
        _viewHandlers["configureAsset"] = args =>
        {
            var assetId = ExtractAssetId(args);
            return RenderView(assetId.HasValue ? $"/catalog/configure?id={assetId.Value}" : "/catalog/configure", args);
        };
        _viewHandlers["userInventory"] = args =>
        {
            var userId = ExtractUserId(args);
            return RenderView(userId.HasValue ? $"/users/{userId.Value}/inventory" : "/home", args);
        };
        _viewHandlers["userFriends"] = args =>
        {
            var userId = ExtractUserId(args);
            return RenderView(userId.HasValue ? $"/users/{userId.Value}/friends" : "/home", args);
        };
        _viewHandlers["myFriends"] = args => RenderView("/users/friends", args);
        _viewHandlers["games"] = args => RenderView("/games", args);
        _viewHandlers["transactions"] = args => RenderView("/transactions", args);
        _viewHandlers["settings"] = args => RenderView("/my/account", args);
        _viewHandlers["catalog"] = args => RenderView("/catalog", args);
        _viewHandlers["catalogItem"] = args =>
        {
            var assetId = ExtractAssetIdFromCatalogArgs(args);
            if (assetId.HasValue)
            {
                var name = ExtractAssetName(args) ?? "--";
                return RenderView($"/catalog/{assetId.Value}/{Uri.EscapeDataString(name)}", args);
            }
            return RenderView("/catalog", args);
        };
        _viewHandlers["myAvatar"] = args => RenderView("/my/avatar", args);
    }

    private static long? ExtractUserId(IEnumerable<dynamic> args)
    {
        try
        {
            var list = args.ToList();
            if (list.Count > 1)
            {
                var arg = list[1];
                try { return Convert.ToInt64(arg?.userId); } catch { }
            }
        }
        catch { }
        return null;
    }

    private static long? ExtractAssetId(IEnumerable<dynamic> args)
    {
        try
        {
            var list = args.ToList();
            if (list.Count > 1)
            {
                var arg = list[1];
                try { return Convert.ToInt64(arg?.assetId); } catch { }
            }
        }
        catch { }
        return null;
    }

    private static long? ExtractAssetIdFromCatalogArgs(IEnumerable<dynamic> args)
    {
        try
        {
            var list = args.ToList();
            if (list.Count > 2)
            {
                var arg = list[2];
                try { return Convert.ToInt64(arg?.assetId); } catch { }
            }
        }
        catch { }
        return null;
    }

    private static string? ExtractAssetName(IEnumerable<dynamic> args)
    {
        try
        {
            var list = args.ToList();
            if (list.Count > 2)
            {
                var arg = list[2];
                try { return Convert.ToString(arg?.name); } catch { }
            }
        }
        catch { }
        return null;
    }

    [HttpPost("/api/get-view")]
    public async Task<IActionResult> GetView([FromQuery] string view)
    {
        string bodyJson;
        using (var reader = new StreamReader(Request.Body, leaveOpen: false))
        {
            bodyJson = await reader.ReadToEndAsync();
        }

        IEnumerable<dynamic> args = Enumerable.Empty<dynamic>();
        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(bodyJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var raw = doc.RootElement.GetRawText();
                    args = JsonSerializer.Deserialize<IEnumerable<dynamic>>(raw) ?? Enumerable.Empty<dynamic>();
                }
            }
            catch
            {
            }
        }

        if (!_viewHandlers.TryGetValue(view, out var handler))
        {
            return NotFound($"Unknown view: {view}");
        }

        try
        {
            var html = await handler(args);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                $"Error rendering view '{view}': {ex.Message}");
        }
    }

    private static async Task<string> RenderView(string relativePath, IEnumerable<dynamic> args)
    {
        var frontendUrl = BuildFrontendUrl(relativePath);

        if (Uri.TryCreate(frontendUrl, UriKind.Absolute, out _))
        {
            try
            {
                using var response = await _frontendClient.GetAsync(frontendUrl, HttpCompletionOption.ResponseContentRead);
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(html))
                    {
                        return html;
                    }
                }
            }
            catch
            {
            }
        }

        return BuildFallbackHtml(relativePath);
    }

    private static string BuildFrontendUrl(string relativePath)
    {
        var path = relativePath.TrimStart('/');
        if (!string.IsNullOrWhiteSpace(_frontendBaseUrl))
        {
            return _frontendBaseUrl + "/" + path;
        }
        return "/" + path;
    }

    private static string BuildFallbackHtml(string relativePath)
    {
        var path = relativePath.StartsWith('/') ? relativePath : "/" + relativePath;
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>error fr</title>
    <meta http-equiv=""refresh"" content=""0; url={path}"">
</head>
<body>
    <script>window.location.replace('{path}');</script>
</body>
</html>";
    }
}
