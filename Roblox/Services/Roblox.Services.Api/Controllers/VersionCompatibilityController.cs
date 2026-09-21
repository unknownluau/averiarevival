using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Roblox.Web.Infrastructure.Controllers;
using Roblox.Web.Infrastructure.Metadata;

namespace Roblox.Services.Api.Controllers;

[ApiController]
[Route("/")]
public class VersionCompatibilityController : RobloxControllerBase
{
    private static readonly IReadOnlyList<string> AllowedMd5Hashes = new[]
    {
        "088e8d2d5d31fd351f66efc7049dab10",
        "bba43f967698feff49038f51b391b48e",
        "4091ce1193a5430573430411eb20bd44",
        "7da7086e7f3a739873fa5970ef586e98",
        "1fd6e7becff68acc140b2db17e24c86e",
    };

    private static readonly IReadOnlyList<string> AllowedSecurityVersions = new[]
    {
        "0.206.0pcplayer",
        "0.235.0pcplayer",
        "0.314.0pcplayer",
        "0.376.0pcplayer",
        "0.355.0pcplayer",
        "2.355.0iosapp",
        "0.395.0pcplayer",
        "0.450.0pcplayer",
        "0.451.0pcplayer",
        "0.463.0pcplayer",
    };

    [RequireRccRequest]
    [HttpGet("GetAllowedMD5Hashes")]
    public dynamic GetAllowedMd5Hashes()
    {
        return new { data = AllowedMd5Hashes };
    }

    [RequireRccRequest]
    [HttpGet("GetAllowedSecurityKeys")]
    public bool GetAllowedSecurityKeys()
    {
        return true;
    }

    [RequireRccRequest]
    [HttpGet("GetAllowedSecurityVersions")]
    public dynamic GetAllowedSecurityVersions()
    {
        return new { data = JsonSerializer.Serialize(AllowedSecurityVersions) };
    }
}
