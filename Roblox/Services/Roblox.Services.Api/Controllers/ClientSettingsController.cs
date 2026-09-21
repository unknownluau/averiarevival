using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class ClientSettingsController : RobloxControllerBase
{
    [HttpPost("Setting/Get/{type}")]
    [HttpPost("Setting/QuietGet/{type}")]
    [HttpGet("Setting/Get/{type}")]
    [HttpGet("Setting/QuietGet/{type}")]
    [AllowRobloxAnonymous]
    public IActionResult GetApplicationSettingsLegacy(string type, string apiKey)
    {
        var featureFlags = GetFeatureFlags(type, apiKey);
        return featureFlags.ErrorMessage != null
            ? BadRequest(new
            {
                errors = new[]
                {
                    new { code = 0, message = featureFlags.ErrorMessage },
                },
            })
            : Content(featureFlags.Content!, "application/json");
    }

    private static string GetTypeForApiKey(string type, string apiKey)
    {
        switch (apiKey)
        {
            case "9CE2063F-BB45-449B-89D4-65CD2ED806CD":
                return "RCCServiceUJ38BA31M8F47VA76XZ1RYONSSTILA3F";
            case "D6925E56-BFB9-4908-AAA2-A5B1EC4B2D79":
            case "08BF6621-8100-4484-B14C-87497E372160":
                return type == "StudioAppSettings"
                    ? type
                    : "ClientAppSettings2017";
            case "D6925E56-BFB9-4908-AAA2-A5B1EC4B2D7A":
                return "RCCService2018";
            case "76E5A40C-3AE1-4028-9F10-7C62520BD94F":
            case "19C0B314-AC23-4CD4-8A37-02C4140F7240":
                return "ClientAppSettings2018";
            default:
                return string.Empty;
        }
    }

    private (string? Content, string? ErrorMessage) GetFeatureFlags(string type, string apiKey)
    {
        type = GetTypeForApiKey(type, apiKey);
        if (string.IsNullOrWhiteSpace(type))
        {
            return (null, $"Invalid API key: {apiKey}");
        }

        var featureFlags = Path.Join(Roblox.Configuration.JsonDataDirectory, $"{type}.json");
        if (!System.IO.File.Exists(featureFlags))
        {
            return (null, $"Feature flags not found for {type}");
        }

        return (FileContentCache.ReadText(featureFlags), null);
    }
}
